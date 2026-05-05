using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class RawAccessPointCandidateServiceTests
{
    [Fact]
    public void GetRawAccessPointCandidates_ReturnsUnresolvedSourceTaggedCandidates()
    {
        var service = new RawAccessPointCandidateService();

        var candidates = service.GetRawAccessPointCandidates();

        Assert.NotEmpty(candidates);
        Assert.Equal(15, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.NotEqual(Guid.Empty, candidate.Id);
            Assert.False(string.IsNullOrWhiteSpace(candidate.Name));
            Assert.False(string.IsNullOrWhiteSpace(candidate.RiverName));
            Assert.False(string.IsNullOrWhiteSpace(candidate.SourceName));
            Assert.False(string.IsNullOrWhiteSpace(candidate.SourceUrl));
            Assert.NotEqual(default, candidate.CapturedAtUtc);
            Assert.False(candidate.IsResolved);
            Assert.Equal(RawAccessPointReviewState.Unreviewed, candidate.ReviewState);
            Assert.Null(candidate.ReviewerNote);
            Assert.True(Enum.IsDefined(candidate.SourceType));
        });
    }

    [Fact]
    public void GetRawAccessPointCandidates_SupportsAllReviewLabels()
    {
        var reviewStates = Enum.GetValues<RawAccessPointReviewState>();

        Assert.Contains(RawAccessPointReviewState.Unreviewed, reviewStates);
        Assert.Contains(RawAccessPointReviewState.NeedsMoreSourceContext, reviewStates);
        Assert.Contains(RawAccessPointReviewState.DuplicateCandidate, reviewStates);
        Assert.Contains(RawAccessPointReviewState.LikelyExistingAccessPoint, reviewStates);
        Assert.Contains(RawAccessPointReviewState.PromotableLater, reviewStates);
        Assert.Contains(RawAccessPointReviewState.OutOfScope, reviewStates);
    }

    [Fact]
    public void UpdateReviewState_ChangesOnlyReviewMetadata_AndKeepsCandidateUnresolved()
    {
        var service = new RawAccessPointCandidateService();
        var candidate = service.GetRawAccessPointCandidates().First();

        var updated = service.UpdateReviewState(
            candidate.Id,
            RawAccessPointReviewState.DuplicateCandidate,
            "  likely same access as another source  ");

        Assert.NotNull(updated);
        Assert.Equal(candidate.Id, updated.Id);
        Assert.Equal(RawAccessPointReviewState.DuplicateCandidate, updated.ReviewState);
        Assert.Equal("likely same access as another source", updated.ReviewerNote);
        Assert.False(updated.IsResolved);
        Assert.Equal(candidate.Name, updated.Name);
        Assert.Equal(candidate.SourceUrl, updated.SourceUrl);
    }

    [Fact]
    public void UpdateReviewState_ReturnsNull_WhenCandidateIsMissing()
    {
        var service = new RawAccessPointCandidateService();

        var updated = service.UpdateReviewState(
            Guid.NewGuid(),
            RawAccessPointReviewState.OutOfScope,
            "not found");

        Assert.Null(updated);
    }

    [Fact]
    public void GetRawAccessPointCandidates_KeepsNeuseAndTributaryCandidatesOnly()
    {
        var service = new RawAccessPointCandidateService();

        var candidates = service.GetRawAccessPointCandidates();

        Assert.All(candidates, candidate =>
            Assert.Contains(candidate.RiverName, new[] { "Neuse River", "Swift Creek", "Trent River" }));
    }

    [Fact]
    public void GetRawAccessPointCandidates_PreservesDuplicates_ForLaterReconciliation()
    {
        var service = new RawAccessPointCandidateService();

        var candidates = service.GetRawAccessPointCandidates();

        Assert.True(candidates.Count(candidate => candidate.Name == "Anderson Point Park") > 1);
        Assert.True(candidates.Count(candidate => candidate.Name == "Cliffs of the Neuse State Park") > 1);
    }

    [Fact]
    public void RawAccessPointCandidate_DoesNotCarryPromotionOrInferenceFields()
    {
        var properties = typeof(RawAccessPointCandidate)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet();

        Assert.DoesNotContain("RiverMile", properties);
        Assert.DoesNotContain("SegmentId", properties);
        Assert.DoesNotContain("AccessPointId", properties);
        Assert.DoesNotContain("StartAccessPointId", properties);
        Assert.DoesNotContain("EndAccessPointId", properties);
        Assert.DoesNotContain("GaugeId", properties);
    }

    [Fact]
    public void RawAccessPointCandidates_AreNotPromotedIntoCatalogAccessPointsOrSegments()
    {
        var rawService = new RawAccessPointCandidateService();
        var catalogService = new SegmentCatalogService();

        var rawCandidateIds = rawService.GetRawAccessPointCandidates().Select(candidate => candidate.Id).ToHashSet();
        var accessPointIds = catalogService.GetPresetAccessPoints().Select(accessPoint => accessPoint.Id).ToHashSet();
        var segmentIds = catalogService.GetPresetSegments().Select(segment => segment.Id).ToHashSet();

        Assert.Empty(rawCandidateIds.Intersect(accessPointIds));
        Assert.Empty(rawCandidateIds.Intersect(segmentIds));

        rawService.UpdateReviewState(
            rawCandidateIds.First(),
            RawAccessPointReviewState.LikelyExistingAccessPoint,
            "review label only");

        Assert.Empty(rawCandidateIds.Intersect(catalogService.GetPresetAccessPoints().Select(accessPoint => accessPoint.Id)));
        Assert.Empty(rawCandidateIds.Intersect(catalogService.GetPresetSegments().Select(segment => segment.Id)));
    }
}
