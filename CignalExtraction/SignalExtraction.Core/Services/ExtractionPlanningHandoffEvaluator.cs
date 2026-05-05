using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class ExtractionPlanningHandoffEvaluator
{
    public ExtractionPlanningHandoffResult Evaluate(ExtractionPlanningHandoffInput input)
    {
        var result = new ExtractionPlanningHandoffResult
        {
            SegmentId = input.SegmentId,
            SegmentName = input.SegmentName,
            DistanceMiles = input.DistanceMiles,
            PaddlingSpeedMph = input.PaddlingSpeedMph,
            RiverCurrentMphOverride = input.RiverCurrentMphOverride,
            LaunchTimeLocal = input.LaunchTimeLocal
        };

        AddExtractionFacts(input.Extraction, result);
        AddPlanningContext(input, result);
        AddMissingInputs(input, result);
        AddAmbiguityFlags(input, result);

        result.CanEstimate = result.MissingInputs.Count == 0 && result.AmbiguityFlags.Count == 0;
        result.Summary = BuildSummary(result);

        return result;
    }

    private static void AddExtractionFacts(ExtractionResult extraction, ExtractionPlanningHandoffResult result)
    {
        if (!string.IsNullOrWhiteSpace(extraction.PutInLocation))
            result.AvailableInputs.Add($"putIn:{extraction.PutInLocation}");

        if (!string.IsNullOrWhiteSpace(extraction.PullOutLocation))
            result.AvailableInputs.Add($"pullOut:{extraction.PullOutLocation}");

        if (!string.IsNullOrWhiteSpace(extraction.WatercraftType))
            result.AvailableInputs.Add($"watercraftType:{extraction.WatercraftType}");

        if (extraction.DurationHours.HasValue)
            result.AvailableInputs.Add($"durationHours:{extraction.DurationHours.Value:0.###}");

        if (!string.IsNullOrWhiteSpace(extraction.TripDateOrTiming))
            result.AvailableInputs.Add($"tripDateOrTiming:{extraction.TripDateOrTiming}");
    }

    private static void AddPlanningContext(ExtractionPlanningHandoffInput input, ExtractionPlanningHandoffResult result)
    {
        if (input.SegmentId.HasValue)
            result.AvailableInputs.Add($"segmentId:{input.SegmentId.Value}");

        if (!string.IsNullOrWhiteSpace(input.SegmentName))
            result.AvailableInputs.Add($"segmentName:{input.SegmentName}");

        if (input.DistanceMiles.HasValue && input.DistanceMiles.Value > 0)
            result.AvailableInputs.Add($"distanceMiles:{input.DistanceMiles.Value:0.###}");

        if (input.PaddlingSpeedMph.HasValue && input.PaddlingSpeedMph.Value > 0)
            result.AvailableInputs.Add($"paddlingSpeedMph:{input.PaddlingSpeedMph.Value:0.###}");

        if (input.RiverCurrentMphOverride.HasValue)
            result.AvailableInputs.Add($"riverCurrentMphOverride:{input.RiverCurrentMphOverride.Value:0.###}");

        if (input.LaunchTimeLocal.HasValue)
            result.AvailableInputs.Add($"launchTimeLocal:{input.LaunchTimeLocal.Value:O}");
    }

    private static void AddMissingInputs(ExtractionPlanningHandoffInput input, ExtractionPlanningHandoffResult result)
    {
        if (!input.SegmentId.HasValue)
            result.MissingInputs.Add("segmentId");

        if (string.IsNullOrWhiteSpace(input.SegmentName))
            result.MissingInputs.Add("segmentName");

        if (!input.DistanceMiles.HasValue || input.DistanceMiles.Value <= 0)
            result.MissingInputs.Add("distanceMiles");

        if (!input.PaddlingSpeedMph.HasValue || input.PaddlingSpeedMph.Value <= 0)
            result.MissingInputs.Add("paddlingSpeedMph");
    }

    private static void AddAmbiguityFlags(ExtractionPlanningHandoffInput input, ExtractionPlanningHandoffResult result)
    {
        if (!input.SegmentId.HasValue &&
            (!string.IsNullOrWhiteSpace(input.Extraction.PutInLocation) ||
             !string.IsNullOrWhiteSpace(input.Extraction.PullOutLocation)))
        {
            result.AmbiguityFlags.Add("routeTextPresentWithoutGroundedSegment");
        }

        if (input.Extraction.NeedsReview)
            result.AmbiguityFlags.Add("extractionNeedsReview");
    }

    private static string BuildSummary(ExtractionPlanningHandoffResult result)
    {
        if (result.CanEstimate)
            return "Planning-ready: required estimate inputs are grounded.";

        var blockers = result.MissingInputs.Concat(result.AmbiguityFlags).ToList();
        return "Not planning-ready: " + string.Join(", ", blockers) + ".";
    }
}
