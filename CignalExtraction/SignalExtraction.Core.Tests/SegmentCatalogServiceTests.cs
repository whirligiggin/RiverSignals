using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class SegmentCatalogServiceTests
{
    [Fact]
    public void GetPresetAccessPoints_ReturnsStablePublicAccesses_ForPriorityWaters()
    {
        var service = new SegmentCatalogService();

        var accessPoints = service.GetPresetAccessPoints();

        Assert.NotEmpty(accessPoints);
        Assert.Equal(29, accessPoints.Count);
        Assert.All(accessPoints, accessPoint =>
        {
            Assert.NotEqual(Guid.Empty, accessPoint.Id);
            Assert.NotEqual(Guid.Empty, accessPoint.RiverId);
            Assert.True(accessPoint.IsPublic);
            Assert.True(accessPoint.RiverOrder > 0);
            Assert.False(string.IsNullOrWhiteSpace(accessPoint.Name));
        });
        Assert.Contains(accessPoints, accessPoint => accessPoint.Name == "Anderson Point Park Boat Launch");
        Assert.Contains(accessPoints, accessPoint => accessPoint.Name == "Poole Road River Access");
        Assert.Contains(accessPoints, accessPoint => accessPoint.Name == "Falls Dam River Access");
        Assert.Contains(accessPoints, accessPoint => accessPoint.Name == "Thornton Road River Access");
        Assert.Contains(accessPoints, accessPoint => accessPoint.Name == "River Bend Park Kayak Launch");
        Assert.Contains(accessPoints, accessPoint => accessPoint.Name == "Clayton River Walk Boat Launch");
    }

    [Fact]
    public void GetPresetAccessPoints_IncludesRaleighNeusePlanningContext()
    {
        var service = new SegmentCatalogService();

        var accessPoints = service.GetPresetAccessPoints();

        var fallsDam = Assert.Single(accessPoints, accessPoint => accessPoint.Name == "Falls Dam River Access");
        Assert.Equal(0.25, fallsDam.RiverMile);
        Assert.Equal("12098 Old Falls of the Neuse Road", fallsDam.Address);
        Assert.Contains("Trailer parking", fallsDam.Amenities);
        Assert.Contains("City of Raleigh", fallsDam.SourceName);

        var pooleRoad = Assert.Single(accessPoints, accessPoint => accessPoint.Name == "Poole Road River Access");
        Assert.Equal(17.7, pooleRoad.RiverMile);
        Assert.Equal("6501 Poole Road", pooleRoad.Address);
        Assert.Contains("Trailer parking", pooleRoad.Amenities);
    }

    [Fact]
    public void GetPresetAccessPoints_IncludesRiverMileProvenance_ForAllAccessPoints()
    {
        var service = new SegmentCatalogService();

        var accessPoints = service.GetPresetAccessPoints();

        Assert.All(accessPoints, accessPoint =>
        {
            Assert.True(accessPoint.RiverMile.HasValue);
            Assert.False(string.IsNullOrWhiteSpace(accessPoint.RiverMileSource));
            Assert.True(accessPoint.RiverMileConfidence is > 0 and <= 1);
            Assert.NotNull(accessPoint.LastReviewedAt);
            Assert.Equal("river-mile-spine-integration-v1 seed", accessPoint.ReviewTrigger);
        });
    }

    [Fact]
    public void GetPresetSegments_ReferencesKnownAccessPoints_AndRiverOrder()
    {
        var service = new SegmentCatalogService();

        var accessPointIds = service.GetPresetAccessPoints().Select(accessPoint => accessPoint.Id).ToHashSet();
        var segments = service.GetPresetSegments();

        Assert.Equal(20, segments.Count);
        Assert.All(segments, segment =>
        {
            Assert.Contains(segment.StartAccessPointId, accessPointIds);
            Assert.Contains(segment.EndAccessPointId, accessPointIds);
            Assert.True(segment.RiverOrder > 0);
            Assert.False(string.IsNullOrWhiteSpace(segment.PutInName));
            Assert.False(string.IsNullOrWhiteSpace(segment.TakeOutName));
        });
    }

    [Fact]
    public void GetPresetSegments_IncludesRaleighNeuseAccessToAccessDistances()
    {
        var service = new SegmentCatalogService();

        var segments = service.GetPresetSegments();

        Assert.Contains(segments, segment =>
            segment.Name == "Neuse River - Falls Dam River Access to Thornton Road River Access" &&
            segment.DistanceMiles == 4.7 &&
            segment.PutInRiverMile == 0.25 &&
            segment.TakeOutRiverMile == 4.5 &&
            segment.PlanningSource != null &&
            segment.PlanningSource.Contains("City of Raleigh"));
        Assert.Contains(segments, segment =>
            segment.Name == "Neuse River - Thornton Road River Access to River Bend Park Kayak Launch" &&
            segment.DistanceMiles == 5.6);
        Assert.Contains(segments, segment =>
            segment.Name == "Neuse River - River Bend Park Kayak Launch to Buffaloe Road River Access" &&
            segment.DistanceMiles == 1.0);
        Assert.Contains(segments, segment =>
            segment.Name == "Neuse River - Buffaloe Road River Access to Anderson Point Park Boat Launch" &&
            segment.DistanceMiles == 6.0);
        Assert.Contains(segments, segment =>
            segment.Name == "Neuse River - Anderson Point Park Boat Launch to Poole Road River Access" &&
            segment.DistanceMiles == 1.5 &&
            segment.PutInAddress == "20 Anderson Point Drive" &&
            segment.TakeOutAddress == "6501 Poole Road");
    }

    [Fact]
    public void GetPresetSegments_KeepsFallsDamToBuffaloeAggregateInactive()
    {
        var service = new SegmentCatalogService();

        var segments = service.GetPresetSegments();

        var aggregate = Assert.Single(segments, segment =>
            segment.Name == "Neuse River - Falls Dam River Access to Buffaloe Road River Access");
        Assert.False(aggregate.IsActive);
        Assert.Equal(new Guid("55555555-5555-5555-5555-555555555555"), aggregate.Id);
    }

    [Fact]
    public void GetPresetUsgsGauges_ReturnsStableRegistry_WithoutForcingFinalLinkage()
    {
        var service = new SegmentCatalogService();

        var riverIds = service.GetPresetRivers().Select(river => river.Id).ToHashSet();
        var gauges = service.GetPresetUsgsGauges();

        Assert.Equal(19, gauges.Count);
        Assert.All(gauges, gauge =>
        {
            Assert.NotEqual(Guid.Empty, gauge.Id);
            Assert.Contains(gauge.RiverId, riverIds);
            Assert.False(string.IsNullOrWhiteSpace(gauge.StationId));
            Assert.False(string.IsNullOrWhiteSpace(gauge.Name));
            Assert.Equal("USGS", gauge.Source);
            Assert.Equal(gauge.StationId, gauge.SourceReference);
        });
        Assert.Contains(gauges, gauge => gauge.Name == "Neuse River | Falls Dam (Raleigh)");
        Assert.Contains(gauges, gauge => gauge.Name == "Little River (Neuse basin) | near Zebulon");
        Assert.Contains(gauges, gauge => gauge.Name == "New Hope River (Jordan Lake inflow) | near Moncure");
    }

    [Fact]
    public void GetPresetUsgsGauges_IncludesRiverMileProvenance_ForAllGauges()
    {
        var service = new SegmentCatalogService();

        var gauges = service.GetPresetUsgsGauges();

        Assert.All(gauges, gauge =>
        {
            Assert.True(gauge.RiverMile.HasValue);
            Assert.False(string.IsNullOrWhiteSpace(gauge.RiverMileSource));
            Assert.True(gauge.RiverMileConfidence is > 0 and <= 1);
            Assert.NotNull(gauge.LastReviewedAt);
            Assert.Equal("river-mile-spine-integration-v1 seed", gauge.ReviewTrigger);
        });
    }

    [Fact]
    public void GetPresetUsgsGaugeImportTargets_ReturnsExplicitReviewableMeaning_Metadata()
    {
        var service = new SegmentCatalogService();

        var segmentIds = service.GetPresetSegments().Select(segment => segment.Id).ToHashSet();
        var gaugeIds = service.GetPresetUsgsGauges().Select(gauge => gauge.Id).ToHashSet();
        var targets = service.GetPresetUsgsGaugeImportTargets();

        Assert.Equal(14, targets.Count);
        Assert.All(targets, target =>
        {
            Assert.Contains(target.GaugeId, gaugeIds);
            Assert.Contains(target.SegmentId, segmentIds);
            Assert.True(Enum.IsDefined(target.RelationshipType));
            Assert.True(Enum.IsDefined(target.ReviewStatus));
            Assert.False(string.IsNullOrWhiteSpace(target.Notes));
        });
        Assert.Contains(targets, target => target.RelationshipType == SignalExtraction.Core.Models.UsgsGaugeRelationshipType.CorridorReference);
        Assert.Contains(targets, target => target.RelationshipType == SignalExtraction.Core.Models.UsgsGaugeRelationshipType.LocalReachReference);
        Assert.All(targets, target => Assert.Equal(SignalExtraction.Core.Models.UsgsGaugeLinkageReviewStatus.Provisional, target.ReviewStatus));
        Assert.All(targets, target =>
        {
            Assert.True(target.MappingConfidence is > 0 and <= 1);
            Assert.False(string.IsNullOrWhiteSpace(target.MappingConfidenceSource));
        });
    }

    [Fact]
    public void GetPresetSegments_AddsParallelRiverMileDistance_WithoutChangingExistingDistance()
    {
        var service = new SegmentCatalogService();

        var segments = service.GetPresetSegments();

        Assert.All(segments, segment =>
        {
            Assert.True(segment.RiverMileDistanceMiles.HasValue);
            Assert.True(segment.DistanceMiles > 0);
        });

        var fallsToThornton = Assert.Single(segments, segment =>
            segment.Name == "Neuse River - Falls Dam River Access to Thornton Road River Access");

        Assert.Equal(4.7, fallsToThornton.DistanceMiles);
        Assert.Equal(4.25, fallsToThornton.RiverMileDistanceMiles);
    }

    [Fact]
    public void GetRiverMileDistanceComparisons_ReturnsCoordinatorReviewArtifact_ForAllSeededSegments()
    {
        var service = new SegmentCatalogService();

        var comparisons = service.GetRiverMileDistanceComparisons();

        Assert.Equal(service.GetPresetSegments().Count, comparisons.Count);
        Assert.All(comparisons, comparison =>
        {
            Assert.NotEqual(Guid.Empty, comparison.SegmentId);
            Assert.True(comparison.ExistingDistance > 0);
            Assert.True(comparison.RiverMileDistance.HasValue);
            Assert.True(comparison.Delta.HasValue);
            Assert.True(Enum.IsDefined(comparison.ReviewClassification));
        });

        var fallsToThornton = Assert.Single(comparisons, comparison =>
            comparison.SegmentId == new Guid("55555555-5555-5555-5555-111111111111"));

        Assert.Equal(4.7, fallsToThornton.ExistingDistance);
        Assert.Equal(4.25, fallsToThornton.RiverMileDistance);
        Assert.Equal(-0.45, fallsToThornton.Delta);
        Assert.Equal(DistanceReconciliationReviewClassification.RequiresReview, fallsToThornton.ReviewClassification);
    }

    [Fact]
    public void GetRiverMileDistanceComparisons_ClassifiesOnlyUnambiguousAlignment()
    {
        var service = new SegmentCatalogService();

        var comparisons = service.GetRiverMileDistanceComparisons();

        Assert.Contains(comparisons, comparison =>
            comparison.Delta == 0 &&
            comparison.ReviewClassification == DistanceReconciliationReviewClassification.Aligned);

        Assert.All(
            comparisons.Where(comparison => comparison.Delta != 0),
            comparison => Assert.Equal(
                DistanceReconciliationReviewClassification.RequiresReview,
                comparison.ReviewClassification));
    }

    [Fact]
    public void Estimate_UsesCatalogDistanceMiles_WhenRiverMileDistanceDiffers()
    {
        var catalogService = new SegmentCatalogService();
        var segment = Assert.Single(catalogService.GetPresetSegments(), segment =>
            segment.Name == "Neuse River - Falls Dam River Access to Thornton Road River Access");
        var estimationService = new TripEstimationService();

        Assert.Equal(4.7, segment.DistanceMiles);
        Assert.Equal(4.25, segment.RiverMileDistanceMiles);

        var estimate = estimationService.Estimate(new TripEstimateRequest
        {
            SegmentId = segment.Id,
            SegmentName = segment.Name,
            DistanceMiles = segment.DistanceMiles,
            PaddlingSpeedMph = 4.7,
            RiverCurrentMphOverride = 0
        });

        Assert.Equal(4.7, estimate.DistanceMiles);
        Assert.Equal(TimeSpan.FromHours(1), estimate.EstimatedDuration);
    }
}
