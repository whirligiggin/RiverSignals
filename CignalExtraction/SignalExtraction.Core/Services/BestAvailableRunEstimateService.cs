using System.Globalization;
using System.Text;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class BestAvailableRunEstimateService : IBestAvailableRunEstimateService
{
    private const double DefaultPaddlingSpeedMph = 3.0;
    private const double ConservativeDefaultDistanceMiles = 5.0;
    private const double BaselineCurrentMph = 0.0;

    private readonly ISegmentCatalogService _segmentCatalogService;
    private readonly ITripEstimationService _tripEstimationService;

    public BestAvailableRunEstimateService(
        ISegmentCatalogService segmentCatalogService,
        ITripEstimationService tripEstimationService)
    {
        _segmentCatalogService = segmentCatalogService;
        _tripEstimationService = tripEstimationService;
    }

    public WideRunRequestSubmissionResult Estimate(WideRunRequest request, StoredWideRunRequest storedRequest)
    {
        var rivers = _segmentCatalogService.GetPresetRivers();
        var river = FindByName(rivers, request.RiverName, item => item.Name);
        if (river == null)
        {
            return new WideRunRequestSubmissionResult(
                storedRequest,
                null,
                new BestAvailableEstimateBasis(
                    CanEstimate: false,
                    Status: "ungrounded",
                    DistanceBasis: "none",
                    DistanceMiles: null,
                    DistanceSource: null,
                    CurrentBasis: "none",
                    MatchedRiverName: null,
                    MatchedRiverId: null,
                    MatchedSegmentId: null,
                    MatchedPutInName: null,
                    MatchedTakeOutName: null,
                    IsProvisional: true,
                    Evidence: ["No known river or alias matched the requested river text."],
                    ReviewFlags: ["needs_river_review"]));
        }

        var riverAccessPoints = _segmentCatalogService
            .GetPresetAccessPoints()
            .Where(accessPoint => accessPoint.RiverId == river.Id)
            .ToList();
        var putInAccessPoint = FindByName(riverAccessPoints, request.PutInText, accessPoint => accessPoint.Name);
        var takeOutAccessPoint = FindByName(riverAccessPoints, request.TakeOutText, accessPoint => accessPoint.Name);
        var activeSegments = _segmentCatalogService
            .GetPresetSegments()
            .Where(segment => segment.IsActive && segment.RiverId == river.Id)
            .ToList();

        var matchedSegment = activeSegments.FirstOrDefault(segment =>
            AccessMatchesSegmentEndpoint(segment.StartAccessPointId, segment.PutInName, putInAccessPoint, request.PutInText) &&
            AccessMatchesSegmentEndpoint(segment.EndAccessPointId, segment.TakeOutName, takeOutAccessPoint, request.TakeOutText));

        var distance = SelectDistance(matchedSegment, putInAccessPoint, takeOutAccessPoint);
        var segmentName = $"{river.Name} - {storedRequest.PutInText} to {storedRequest.TakeOutText}";
        var estimate = _tripEstimationService.Estimate(new TripEstimateRequest
        {
            SegmentId = matchedSegment?.Id,
            SegmentName = matchedSegment?.Name ?? segmentName,
            DistanceMiles = distance.DistanceMiles,
            PaddlingSpeedMph = DefaultPaddlingSpeedMph,
            RiverCurrentMphOverride = matchedSegment?.DefaultCurrentMph ?? BaselineCurrentMph
        });

        var evidence = BuildEvidence(river, matchedSegment, putInAccessPoint, takeOutAccessPoint, distance);
        var reviewFlags = BuildReviewFlags(matchedSegment, putInAccessPoint, takeOutAccessPoint, distance);

        return new WideRunRequestSubmissionResult(
            storedRequest,
            estimate,
            new BestAvailableEstimateBasis(
                CanEstimate: true,
                Status: matchedSegment == null ? "provisional_estimate" : "seeded_match_estimate",
                DistanceBasis: distance.Basis,
                DistanceMiles: distance.DistanceMiles,
                DistanceSource: distance.Source,
                CurrentBasis: estimate.CurrentSource,
                MatchedRiverName: river.Name,
                MatchedRiverId: river.Id,
                MatchedSegmentId: matchedSegment?.Id,
                MatchedPutInName: putInAccessPoint?.Name,
                MatchedTakeOutName: takeOutAccessPoint?.Name,
                IsProvisional: true,
                Evidence: evidence,
                ReviewFlags: reviewFlags));
    }

    private static DistanceSelection SelectDistance(
        Segment? matchedSegment,
        AccessPoint? putInAccessPoint,
        AccessPoint? takeOutAccessPoint)
    {
        if (matchedSegment != null)
        {
            return new DistanceSelection(
                matchedSegment.DistanceMiles,
                "curated_run_distance",
                matchedSegment.DistanceSource ?? "seeded segment catalog");
        }

        if (putInAccessPoint?.RiverMile.HasValue == true &&
            takeOutAccessPoint?.RiverMile.HasValue == true)
        {
            var riverMileDelta = Math.Round(
                Math.Abs(takeOutAccessPoint.RiverMile.Value - putInAccessPoint.RiverMile.Value),
                2);

            if (riverMileDelta > 0)
            {
                return new DistanceSelection(
                    riverMileDelta,
                    "grounded_river_mile_delta",
                    "matched access-point river-mile anchors");
            }
        }

        return new DistanceSelection(
            ConservativeDefaultDistanceMiles,
            "conservative_default_assumption",
            "default requested-run assumption");
    }

    private static IReadOnlyList<string> BuildEvidence(
        River river,
        Segment? matchedSegment,
        AccessPoint? putInAccessPoint,
        AccessPoint? takeOutAccessPoint,
        DistanceSelection distance)
    {
        var evidence = new List<string>
        {
            $"Matched river: {river.Name}.",
            $"Distance basis: {distance.Basis}."
        };

        if (matchedSegment != null)
            evidence.Add($"Matched active seeded run: {matchedSegment.Name}.");
        if (putInAccessPoint != null)
            evidence.Add($"Matched put-in candidate: {putInAccessPoint.Name}.");
        if (takeOutAccessPoint != null)
            evidence.Add($"Matched take-out candidate: {takeOutAccessPoint.Name}.");

        return evidence;
    }

    private static IReadOnlyList<string> BuildReviewFlags(
        Segment? matchedSegment,
        AccessPoint? putInAccessPoint,
        AccessPoint? takeOutAccessPoint,
        DistanceSelection distance)
    {
        var flags = new List<string>();

        if (matchedSegment == null)
            flags.Add("needs_run_review");
        if (putInAccessPoint == null)
            flags.Add("needs_put_in_review");
        if (takeOutAccessPoint == null)
            flags.Add("needs_take_out_review");
        if (distance.Basis == "conservative_default_assumption")
            flags.Add("needs_distance_review");

        return flags;
    }

    private static bool AccessMatchesSegmentEndpoint(
        Guid segmentAccessPointId,
        string segmentAccessPointName,
        AccessPoint? matchedAccessPoint,
        string? requestedText)
    {
        if (matchedAccessPoint?.Id == segmentAccessPointId)
            return true;

        return TextMatches(segmentAccessPointName, requestedText);
    }

    private static T? FindByName<T>(IEnumerable<T> items, string? requestedText, Func<T, string> getName)
        where T : class
    {
        return items.FirstOrDefault(item => TextMatches(getName(item), requestedText));
    }

    private static bool TextMatches(string knownName, string? requestedText)
    {
        if (string.IsNullOrWhiteSpace(requestedText))
            return false;

        var known = Normalize(knownName);
        var requested = Normalize(requestedText);

        return known == requested ||
            known.Contains(requested, StringComparison.Ordinal) ||
            requested.Contains(known, StringComparison.Ordinal);
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (char.IsWhiteSpace(character) || char.GetUnicodeCategory(character) == UnicodeCategory.DashPunctuation)
            {
                builder.Append(' ');
            }
        }

        return string.Join(
            ' ',
            builder
                .ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private sealed record DistanceSelection(double DistanceMiles, string Basis, string Source);
}
