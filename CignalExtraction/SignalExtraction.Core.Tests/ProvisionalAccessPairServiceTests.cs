using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class ProvisionalAccessPairServiceTests
{
    [Fact]
    public void GetProvisionalAccessPairs_ReturnsBoundedPutInTakeOutPairs()
    {
        var service = new ProvisionalAccessPairService();

        var pairs = service.GetProvisionalAccessPairs();

        Assert.NotEmpty(pairs);
        Assert.All(pairs, pair =>
        {
            Assert.NotEqual(Guid.Empty, pair.Id);
            Assert.True(pair.PutIn.RawAccessPointCandidateId.HasValue || !string.IsNullOrWhiteSpace(pair.PutIn.PossibleAccessKey));
            Assert.True(pair.TakeOut.RawAccessPointCandidateId.HasValue || !string.IsNullOrWhiteSpace(pair.TakeOut.PossibleAccessKey));
            Assert.True(Enum.IsDefined(pair.PutIn.Basis));
            Assert.True(Enum.IsDefined(pair.TakeOut.Basis));
            Assert.True(Enum.IsDefined(pair.DistanceBasis));
            Assert.NotEqual(default, pair.CreatedAtUtc);
            Assert.False(string.IsNullOrWhiteSpace(pair.SourceName));
        });
    }

    [Fact]
    public void RecordProvisionalAccessPair_PreservesReferencesAndExplicitDistanceBasis()
    {
        var service = new ProvisionalAccessPairService();

        var recorded = service.RecordProvisionalAccessPair(new ProvisionalAccessPair
        {
            PutIn = new ProvisionalAccessReference
            {
                RawAccessPointCandidateId = new Guid("a1000000-0000-0000-0000-000000000006"),
                PossibleAccessKey = " possible-access:cliffs-of-the-neuse-state-park ",
                Basis = ProvisionalAccessReferenceBasis.PossibleAccessKey,
                Label = " Cliffs of the Neuse "
            },
            TakeOut = new ProvisionalAccessReference
            {
                RawAccessPointCandidateId = new Guid("a1000000-0000-0000-0000-000000000005"),
                PossibleAccessKey = " raw-candidate:seven-springs-ncwrc-access ",
                Basis = ProvisionalAccessReferenceBasis.RawCandidate,
                Label = " Seven Springs "
            },
            DistanceMiles = 3.2,
            DistanceBasis = ProvisionalAccessPairDistanceBasis.UserSupplied,
            DistanceSourceName = " UX test participant ",
            CreatedAtUtc = new DateTime(2026, 4, 28, 14, 0, 0, DateTimeKind.Utc),
            SourceName = " UX planning test ",
            SourceReference = " session-2 ",
            Note = " provisional planning context "
        });

        Assert.NotEqual(Guid.Empty, recorded.Id);
        Assert.Equal("possible-access:cliffs-of-the-neuse-state-park", recorded.PutIn.PossibleAccessKey);
        Assert.Equal("raw-candidate:seven-springs-ncwrc-access", recorded.TakeOut.PossibleAccessKey);
        Assert.Equal("Cliffs of the Neuse", recorded.PutIn.Label);
        Assert.Equal("Seven Springs", recorded.TakeOut.Label);
        Assert.Equal(3.2, recorded.DistanceMiles);
        Assert.Equal(ProvisionalAccessPairDistanceBasis.UserSupplied, recorded.DistanceBasis);
        Assert.Equal("UX test participant", recorded.DistanceSourceName);
        Assert.Equal("UX planning test", recorded.SourceName);
        Assert.Equal("session-2", recorded.SourceReference);
        Assert.Equal("provisional planning context", recorded.Note);
        Assert.Contains(service.GetProvisionalAccessPairs(), pair => pair.Id == recorded.Id);
    }

    [Fact]
    public void RecordProvisionalAccessPair_RequiresBothAccessReferences()
    {
        var service = new ProvisionalAccessPairService();

        Assert.Throws<ArgumentException>(() => service.RecordProvisionalAccessPair(new ProvisionalAccessPair
        {
            PutIn = new ProvisionalAccessReference(),
            TakeOut = new ProvisionalAccessReference
            {
                PossibleAccessKey = "raw-candidate:seven-springs-ncwrc-access"
            },
            CreatedAtUtc = new DateTime(2026, 4, 28, 14, 0, 0, DateTimeKind.Utc),
            SourceName = "Internal steward planning"
        }));
    }

    [Fact]
    public void RecordProvisionalAccessPair_RequiresExplicitDistanceBasisWhenDistanceIsProvided()
    {
        var service = new ProvisionalAccessPairService();

        Assert.Throws<ArgumentException>(() => service.RecordProvisionalAccessPair(new ProvisionalAccessPair
        {
            PutIn = new ProvisionalAccessReference { PossibleAccessKey = "possible-access:cliffs-of-the-neuse-state-park" },
            TakeOut = new ProvisionalAccessReference { PossibleAccessKey = "raw-candidate:seven-springs-ncwrc-access" },
            DistanceMiles = 3.2,
            DistanceBasis = ProvisionalAccessPairDistanceBasis.NotProvided,
            CreatedAtUtc = new DateTime(2026, 4, 28, 14, 0, 0, DateTimeKind.Utc),
            SourceName = "Internal steward planning"
        }));
    }

    [Fact]
    public void ProvisionalAccessPair_DoesNotCarryDurableRunPlanningOrHydrologyFields()
    {
        var pairProperties = typeof(ProvisionalAccessPair)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet();
        var referenceProperties = typeof(ProvisionalAccessReference)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet();

        Assert.DoesNotContain("SegmentId", pairProperties);
        Assert.DoesNotContain("StartAccessPointId", pairProperties);
        Assert.DoesNotContain("EndAccessPointId", pairProperties);
        Assert.DoesNotContain("CatalogAccessPointId", pairProperties);
        Assert.DoesNotContain("RiverMile", pairProperties);
        Assert.DoesNotContain("GaugeId", pairProperties);
        Assert.DoesNotContain("FlowRateCfs", pairProperties);
        Assert.DoesNotContain("EstimatedCurrentMph", pairProperties);
        Assert.DoesNotContain("CatalogAccessPointId", referenceProperties);
        Assert.DoesNotContain("RiverMile", referenceProperties);
    }

    [Fact]
    public void ProvisionalAccessPairs_DoNotCreateCatalogAccessPointsOrSegments()
    {
        var pairService = new ProvisionalAccessPairService();
        var catalogService = new SegmentCatalogService();

        var beforeAccessPointIds = catalogService.GetPresetAccessPoints().Select(accessPoint => accessPoint.Id).ToList();
        var beforeSegmentIds = catalogService.GetPresetSegments().Select(segment => segment.Id).ToList();

        pairService.RecordProvisionalAccessPair(new ProvisionalAccessPair
        {
            PutIn = new ProvisionalAccessReference { PossibleAccessKey = "possible-access:cliffs-of-the-neuse-state-park" },
            TakeOut = new ProvisionalAccessReference { PossibleAccessKey = "raw-candidate:seven-springs-ncwrc-access" },
            CreatedAtUtc = new DateTime(2026, 4, 28, 14, 0, 0, DateTimeKind.Utc),
            SourceName = "Internal steward planning"
        });

        Assert.Equal(beforeAccessPointIds, catalogService.GetPresetAccessPoints().Select(accessPoint => accessPoint.Id).ToList());
        Assert.Equal(beforeSegmentIds, catalogService.GetPresetSegments().Select(segment => segment.Id).ToList());
    }

    [Fact]
    public void ProvisionalAccessPairs_DoNotAffectExistingSegmentEstimates()
    {
        var pairService = new ProvisionalAccessPairService();
        var catalogService = new SegmentCatalogService();
        var estimationService = new TripEstimationService();
        var segment = catalogService.GetPresetSegments().First(segment => segment.IsActive);

        var before = estimationService.Estimate(new TripEstimateRequest
        {
            SegmentId = segment.Id,
            SegmentName = segment.Name,
            DistanceMiles = segment.DistanceMiles,
            PaddlingSpeedMph = 3.0,
            RiverCurrentMphOverride = segment.DefaultCurrentMph
        });

        pairService.RecordProvisionalAccessPair(new ProvisionalAccessPair
        {
            PutIn = new ProvisionalAccessReference { PossibleAccessKey = "possible-access:cliffs-of-the-neuse-state-park" },
            TakeOut = new ProvisionalAccessReference { PossibleAccessKey = "raw-candidate:seven-springs-ncwrc-access" },
            CreatedAtUtc = new DateTime(2026, 4, 28, 14, 0, 0, DateTimeKind.Utc),
            SourceName = "Internal steward planning"
        });

        var after = estimationService.Estimate(new TripEstimateRequest
        {
            SegmentId = segment.Id,
            SegmentName = segment.Name,
            DistanceMiles = segment.DistanceMiles,
            PaddlingSpeedMph = 3.0,
            RiverCurrentMphOverride = segment.DefaultCurrentMph
        });

        Assert.Equal(before.EstimatedDuration, after.EstimatedDuration);
        Assert.Equal(before.EffectiveSpeedMph, after.EffectiveSpeedMph);
        Assert.Equal(before.ConditionBasis, after.ConditionBasis);
    }
}
