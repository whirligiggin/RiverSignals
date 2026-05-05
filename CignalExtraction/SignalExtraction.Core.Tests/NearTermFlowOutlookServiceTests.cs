using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class NearTermFlowOutlookServiceTests
{
    [Fact]
    public void GetTomorrowMorningOutlook_ReturnsOutlook_ForExplicitMappedSegmentWithRecentUsgsCurrent()
    {
        var flowService = new InMemoryFlowReadingService();
        var segmentCatalog = new SegmentCatalogService();
        var segmentId = new Guid("33333333-3333-3333-3333-333333333333");
        var observedAtUtc = new DateTime(2026, 4, 22, 14, 0, 0, DateTimeKind.Utc);

        flowService.AddFlowReading(new FlowReading
        {
            Id = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
            SegmentId = segmentId,
            ObservedAtUtc = observedAtUtc,
            EstimatedCurrentMph = 2.1,
            Source = "USGS"
        });

        var service = new NearTermFlowOutlookService(segmentCatalog, flowService);

        var result = service.GetTomorrowMorningOutlook(new TripEstimateRequest
        {
            SegmentId = segmentId,
            LaunchTimeLocal = new DateTime(2026, 4, 23, 9, 0, 0)
        });

        Assert.NotNull(result);
        Assert.Equal(segmentId, result.SegmentId);
        Assert.Equal(2.1, result.EstimatedCurrentMph);
        Assert.Equal("tomorrow-morning outlook", result.Source);
        Assert.Equal(observedAtUtc, result.BasedOnObservedAtUtc);
        Assert.Equal(new Guid("aaaaaaaa-0000-0000-0000-000000000001"), result.BasedOnFlowReadingId);
    }

    [Fact]
    public void GetTomorrowMorningOutlook_ReturnsNull_WhenSegmentHasNoExplicitGaugeMapping()
    {
        var flowService = new InMemoryFlowReadingService();
        var segmentCatalog = new SegmentCatalogService();
        var segmentId = Guid.NewGuid();

        flowService.AddFlowReading(new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = new DateTime(2026, 4, 22, 14, 0, 0, DateTimeKind.Utc),
            EstimatedCurrentMph = 2.1,
            Source = "USGS"
        });

        var service = new NearTermFlowOutlookService(segmentCatalog, flowService);

        var result = service.GetTomorrowMorningOutlook(new TripEstimateRequest
        {
            SegmentId = segmentId,
            LaunchTimeLocal = new DateTime(2026, 4, 23, 9, 0, 0)
        });

        Assert.Null(result);
    }

    [Fact]
    public void GetTomorrowMorningOutlook_ReturnsNull_WhenLatestReadingDoesNotContainCurrent()
    {
        var flowService = new InMemoryFlowReadingService();
        var segmentCatalog = new SegmentCatalogService();
        var segmentId = new Guid("33333333-3333-3333-3333-333333333333");

        flowService.AddFlowReading(new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = new DateTime(2026, 4, 22, 14, 0, 0, DateTimeKind.Utc),
            EstimatedCurrentMph = null,
            FlowRateCfs = 2500,
            Source = "USGS"
        });

        var service = new NearTermFlowOutlookService(segmentCatalog, flowService);

        var result = service.GetTomorrowMorningOutlook(new TripEstimateRequest
        {
            SegmentId = segmentId,
            LaunchTimeLocal = new DateTime(2026, 4, 23, 9, 0, 0)
        });

        Assert.Null(result);
    }
}
