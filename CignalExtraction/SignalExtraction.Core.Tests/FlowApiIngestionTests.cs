using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class FlowApiIngestionTests
{
    [Fact]
    public async Task GetFlow_ReturnsLatestReading_ForSegment()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segmentId = Guid.NewGuid();

        flowService.AddFlowReading(new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc),
            EstimatedCurrentMph = 1.3,
            FlowRateCfs = 1200,
            Source = "OldGauge"
        });

        var latestReading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = new DateTime(2026, 4, 22, 15, 0, 0, DateTimeKind.Utc),
            EstimatedCurrentMph = 2.7,
            GaugeHeightFeet = 4.1,
            FlowRateCfs = 2800,
            Source = "USGS",
            SourceReference = "Gauge_12345"
        };
        flowService.AddFlowReading(latestReading);

        var response = await client.GetAsync($"/api/flow/{segmentId}");

        response.EnsureSuccessStatusCode();
        var returned = await response.Content.ReadFromJsonAsync<FlowReadingApiResponse>();

        Assert.NotNull(returned);
        Assert.Equal(latestReading.Id, returned.Id);
        Assert.Equal(segmentId, returned.SegmentId);
        Assert.Equal(latestReading.ObservedAtUtc, returned.ObservedAtUtc);
        Assert.Equal(2.7, returned.EstimatedCurrentMph);
        Assert.Equal(4.1, returned.GaugeHeightFeet);
        Assert.Equal(2800, returned.FlowRateCfs);
        Assert.Equal("USGS", returned.Source);
        Assert.Equal("Gauge_12345", returned.SourceReference);
    }

    [Fact]
    public async Task GetFlow_ReturnsNotFound_WhenSegmentHasNoReading()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/flow/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostThenGetFlow_ReturnsIngestedReading()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segmentId = Guid.NewGuid();

        var postResponse = await client.PostAsJsonAsync("/api/flow", new
        {
            segmentId,
            observedAtUtc = new DateTime(2026, 4, 22, 15, 0, 0, DateTimeKind.Utc),
            flowRateCfs = 2400,
            estimatedCurrentMph = 2.4,
            source = "USGS",
            sourceReference = "Gauge_12345"
        });

        postResponse.EnsureSuccessStatusCode();

        var getResponse = await client.GetAsync($"/api/flow/{segmentId}");

        getResponse.EnsureSuccessStatusCode();
        var returned = await getResponse.Content.ReadFromJsonAsync<FlowReadingApiResponse>();

        Assert.NotNull(returned);
        Assert.Equal(segmentId, returned.SegmentId);
        Assert.Equal(2.4, returned.EstimatedCurrentMph);
        Assert.Equal(2400, returned.FlowRateCfs);
        Assert.Equal("USGS", returned.Source);
        Assert.Equal("Gauge_12345", returned.SourceReference);
    }

    [Fact]
    public async Task PostFlow_StoresReading_ForLatestSegmentLookup()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segmentId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/flow", new
        {
            segmentId,
            observedAtUtc = new DateTime(2026, 4, 22, 15, 0, 0, DateTimeKind.Utc),
            flowRateCfs = 2400,
            estimatedCurrentMph = 2.4,
            source = "USGS",
            sourceReference = "Gauge_12345"
        });

        response.EnsureSuccessStatusCode();
        var stored = flowService.GetLatestForSegment(segmentId);

        Assert.NotNull(stored);
        Assert.Equal(segmentId, stored.SegmentId);
        Assert.Equal(2.4, stored.EstimatedCurrentMph);
        Assert.Equal(2400, stored.FlowRateCfs);
        Assert.Equal("USGS", stored.Source);
        Assert.Equal("Gauge_12345", stored.SourceReference);
    }

    [Fact]
    public async Task PostFlow_LatestLookup_ReturnsMostRecentReading()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segmentId = Guid.NewGuid();

        await client.PostAsJsonAsync("/api/flow", new
        {
            segmentId,
            observedAtUtc = new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc),
            estimatedCurrentMph = 1.1,
            source = "Gauge"
        });

        await client.PostAsJsonAsync("/api/flow", new
        {
            segmentId,
            observedAtUtc = new DateTime(2026, 4, 22, 14, 0, 0, DateTimeKind.Utc),
            estimatedCurrentMph = 2.6,
            source = "Gauge"
        });

        var latest = flowService.GetLatestForSegment(segmentId);

        Assert.NotNull(latest);
        Assert.Equal(2.6, latest.EstimatedCurrentMph);
    }

    [Fact]
    public async Task PostFlow_InvalidPayload_DoesNotStoreReading()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segmentId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/flow", new
        {
            segmentId,
            observedAtUtc = new DateTime(2026, 4, 22, 15, 0, 0, DateTimeKind.Utc),
            source = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(flowService.GetLatestForSegment(segmentId));
    }

    [Fact]
    public async Task PostFlow_DoesNotChangeExistingEstimationFallbackBehavior()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segmentId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/flow", new
        {
            segmentId,
            observedAtUtc = new DateTime(2026, 4, 22, 15, 0, 0, DateTimeKind.Utc),
            flowRateCfs = 2400,
            source = "Gauge"
        });

        response.EnsureSuccessStatusCode();

        var estimator = new TripEstimationService(flowService);
        var estimate = estimator.Estimate(new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Test Segment",
            DistanceMiles = 8,
            PaddlingSpeedMph = 4,
            RiverCurrentMphOverride = 1.5
        });

        Assert.Equal(1.5, estimate.RiverCurrentMphUsed);
        Assert.Null(estimate.UsedFlowReadingId);
    }

    private static WebApplicationFactory<Program> CreateFactory(IFlowReadingService flowService)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        service => service.ServiceType == typeof(IFlowReadingService));

                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddSingleton(flowService);
                });
            });
    }

    private sealed class FlowReadingApiResponse
    {
        public Guid Id { get; set; }
        public Guid SegmentId { get; set; }
        public DateTime ObservedAtUtc { get; set; }
        public double? EstimatedCurrentMph { get; set; }
        public double? GaugeHeightFeet { get; set; }
        public double? FlowRateCfs { get; set; }
        public string Source { get; set; } = string.Empty;
        public string? SourceReference { get; set; }
    }
}
