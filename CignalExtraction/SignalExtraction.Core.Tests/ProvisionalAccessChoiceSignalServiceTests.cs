using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class ProvisionalAccessChoiceSignalServiceTests
{
    [Fact]
    public void GetProvisionalAccessChoiceSignals_ReturnsBoundedSelectionSignals()
    {
        var service = new ProvisionalAccessChoiceSignalService();

        var signals = service.GetProvisionalAccessChoiceSignals();

        Assert.NotEmpty(signals);
        Assert.All(signals, signal =>
        {
            Assert.NotEqual(Guid.Empty, signal.Id);
            Assert.True(signal.RawAccessPointCandidateId.HasValue || !string.IsNullOrWhiteSpace(signal.PossibleAccessKey));
            Assert.True(Enum.IsDefined(signal.SignalSource));
            Assert.NotEqual(default, signal.SelectedAtUtc);
            Assert.False(string.IsNullOrWhiteSpace(signal.SourceName));
        });
    }

    [Fact]
    public void RecordChoiceSignal_PreservesRawCandidateAndPossibleAccessReference()
    {
        var service = new ProvisionalAccessChoiceSignalService();
        var candidateId = new Guid("a1000000-0000-0000-0000-000000000006");

        var recorded = service.RecordChoiceSignal(new ProvisionalAccessChoiceSignal
        {
            RawAccessPointCandidateId = candidateId,
            PossibleAccessKey = " possible-access:cliffs-of-the-neuse-state-park ",
            SignalSource = ProvisionalAccessChoiceSignalSource.UserSelected,
            SelectedAtUtc = new DateTime(2026, 4, 28, 12, 30, 0, DateTimeKind.Utc),
            SourceName = " UX test participant ",
            SourceReference = " session-1 ",
            Note = " selected during testing "
        });

        Assert.NotEqual(Guid.Empty, recorded.Id);
        Assert.Equal(candidateId, recorded.RawAccessPointCandidateId);
        Assert.Equal("possible-access:cliffs-of-the-neuse-state-park", recorded.PossibleAccessKey);
        Assert.Equal(ProvisionalAccessChoiceSignalSource.UserSelected, recorded.SignalSource);
        Assert.Equal("UX test participant", recorded.SourceName);
        Assert.Equal("session-1", recorded.SourceReference);
        Assert.Equal("selected during testing", recorded.Note);
        Assert.Contains(service.GetProvisionalAccessChoiceSignals(), signal => signal.Id == recorded.Id);
    }

    [Fact]
    public void RecordChoiceSignal_RequiresCandidateOrPossibleAccessReference()
    {
        var service = new ProvisionalAccessChoiceSignalService();

        Assert.Throws<ArgumentException>(() => service.RecordChoiceSignal(new ProvisionalAccessChoiceSignal
        {
            SignalSource = ProvisionalAccessChoiceSignalSource.StewardSelected,
            SelectedAtUtc = new DateTime(2026, 4, 28, 12, 30, 0, DateTimeKind.Utc),
            SourceName = "Internal steward review"
        }));
    }

    [Fact]
    public void RecordChoiceSignal_RequiresSelectionTimestamp()
    {
        var service = new ProvisionalAccessChoiceSignalService();

        Assert.Throws<ArgumentException>(() => service.RecordChoiceSignal(new ProvisionalAccessChoiceSignal
        {
            RawAccessPointCandidateId = new Guid("a1000000-0000-0000-0000-000000000002"),
            SignalSource = ProvisionalAccessChoiceSignalSource.StewardSelected,
            SourceName = "Internal steward review"
        }));
    }

    [Fact]
    public void ProvisionalAccessChoiceSignal_DoesNotCarryPromotionPlanningOrHydrologyFields()
    {
        var properties = typeof(ProvisionalAccessChoiceSignal)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet();

        Assert.DoesNotContain("ReviewState", properties);
        Assert.DoesNotContain("IdentifierType", properties);
        Assert.DoesNotContain("CatalogAccessPointId", properties);
        Assert.DoesNotContain("RiverMile", properties);
        Assert.DoesNotContain("SegmentId", properties);
        Assert.DoesNotContain("StartAccessPointId", properties);
        Assert.DoesNotContain("EndAccessPointId", properties);
        Assert.DoesNotContain("GaugeId", properties);
    }

    [Fact]
    public void ChoiceSignals_DoNotPromoteCandidatesIntoCatalogAccessPointsOrSegments()
    {
        var choiceSignalService = new ProvisionalAccessChoiceSignalService();
        var catalogService = new SegmentCatalogService();

        var rawCandidateIds = choiceSignalService.GetProvisionalAccessChoiceSignals()
            .Where(signal => signal.RawAccessPointCandidateId.HasValue)
            .Select(signal => signal.RawAccessPointCandidateId!.Value)
            .ToHashSet();
        var accessPointIds = catalogService.GetPresetAccessPoints().Select(accessPoint => accessPoint.Id).ToHashSet();
        var segmentIds = catalogService.GetPresetSegments().Select(segment => segment.Id).ToHashSet();

        Assert.Empty(rawCandidateIds.Intersect(accessPointIds));
        Assert.Empty(rawCandidateIds.Intersect(segmentIds));
    }

    [Fact]
    public void ChoiceSignals_DoNotChangeRawCandidateReviewState()
    {
        var choiceSignalService = new ProvisionalAccessChoiceSignalService();
        var rawCandidateService = new RawAccessPointCandidateService();
        var candidateId = new Guid("a1000000-0000-0000-0000-000000000002");

        choiceSignalService.RecordChoiceSignal(new ProvisionalAccessChoiceSignal
        {
            RawAccessPointCandidateId = candidateId,
            SignalSource = ProvisionalAccessChoiceSignalSource.StewardSelected,
            SelectedAtUtc = new DateTime(2026, 4, 28, 12, 30, 0, DateTimeKind.Utc),
            SourceName = "Internal steward review"
        });

        var candidate = rawCandidateService
            .GetRawAccessPointCandidates()
            .Single(candidate => candidate.Id == candidateId);
        Assert.Equal(RawAccessPointReviewState.Unreviewed, candidate.ReviewState);
        Assert.False(candidate.IsResolved);
        Assert.Null(candidate.ReviewerNote);
    }
}
