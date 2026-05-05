using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class SegmentPlanningApiTests
{
    [Fact]
    public async Task GetSegments_ReturnsPresetSegments()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/segments");

        response.EnsureSuccessStatusCode();
        var segments = await response.Content.ReadFromJsonAsync<List<SegmentApiResponse>>();

        Assert.NotNull(segments);
        Assert.NotEmpty(segments);
        Assert.Equal(19, segments.Count);
        Assert.Equal(10, segments.Select(segment => segment.RiverId).Distinct().Count());
        Assert.Contains(segments, segment => segment.Name == "Haw River - Bynum River Access to US 64 River Access");
        Assert.Contains(segments, segment => segment.Name == "Cape Fear River - Buckhorn River Access to Avent Ferry River Access");
        Assert.Contains(segments, segment => segment.Name == "Neuse River - Anderson Point Park Boat Launch to Poole Road River Access");
        Assert.Contains(segments, segment => segment.Name == "Neuse River - Falls Dam River Access to Thornton Road River Access" && segment.DistanceMiles == 4.7);
        Assert.Contains(segments, segment => segment.Name == "Neuse River - Thornton Road River Access to River Bend Park Kayak Launch" && segment.DistanceMiles == 5.6);
        Assert.Contains(segments, segment => segment.Name == "Neuse River - River Bend Park Kayak Launch to Buffaloe Road River Access" && segment.DistanceMiles == 1.0);
        Assert.Contains(segments, segment => segment.Name == "Neuse River - Buffaloe Road River Access to Anderson Point Park Boat Launch" && segment.DistanceMiles == 6.0);
        Assert.DoesNotContain(segments, segment => segment.Name == "Neuse River - Falls Dam River Access to Buffaloe Road River Access");
        Assert.Contains(segments, segment => segment.Name == "Haw River - Saxapahaw River Access to Bynum River Access");
        Assert.Contains(segments, segment => segment.Name == "Tar River - Louisburg River Access to Franklinton River Access");
        Assert.Contains(segments, segment => segment.Name == "Neuse River - Buffaloe Road River Access to Milburnie Dam River Access" && segment.DefaultCurrentMph == 2.5);
        Assert.Contains(segments, segment => segment.Name == "Swift Creek - Lake Wheeler Road River Access to Yates Mill Area River Access" && segment.DefaultCurrentMph == 2.5);
        Assert.All(segments, segment => Assert.NotEqual(Guid.Empty, segment.RiverId));
    }

    [Fact]
    public async Task GetSegments_DoesNotExposeParallelRiverMileDistance()
    {
        await using var factory = CreateFactory(new InMemoryFlowReadingService());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/segments");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.All(document.RootElement.EnumerateArray(), segment =>
        {
            Assert.False(segment.TryGetProperty("riverMileDistanceMiles", out _));
        });
    }

    [Fact]
    public async Task GetSegments_PreservesLegacyGuidToNameMappings()
    {
        await using var factory = CreateFactory(new InMemoryFlowReadingService());
        using var client = factory.CreateClient();

        var segments = await client.GetFromJsonAsync<List<SegmentApiResponse>>("/api/segments");

        Assert.NotNull(segments);
        Assert.Contains(segments, segment => segment.Id == new Guid("11111111-1111-1111-1111-111111111111") && segment.Name == "Haw River - Bynum River Access to US 64 River Access");
        Assert.Contains(segments, segment => segment.Id == new Guid("22222222-2222-2222-2222-222222222222") && segment.Name == "Cape Fear River - Buckhorn River Access to Avent Ferry River Access");
        Assert.Contains(segments, segment => segment.Id == new Guid("33333333-3333-3333-3333-333333333333") && segment.Name == "Neuse River - Anderson Point Park Boat Launch to Poole Road River Access");
        Assert.Contains(segments, segment => segment.Id == new Guid("44444444-4444-4444-4444-444444444444") && segment.Name == "Cape Fear River - Lillington River Access to Erwin River Access");
    }

    [Fact]
    public async Task GetSegments_AssignsDistinctNewIdsToPrioritySegments()
    {
        await using var factory = CreateFactory(new InMemoryFlowReadingService());
        using var client = factory.CreateClient();

        var segments = await client.GetFromJsonAsync<List<SegmentApiResponse>>("/api/segments");

        Assert.NotNull(segments);
        Assert.DoesNotContain(segments, segment => segment.Id == new Guid("55555555-5555-5555-5555-555555555555"));
        Assert.Contains(segments, segment => segment.Id == new Guid("55555555-5555-5555-5555-111111111111"));
        Assert.Contains(segments, segment => segment.Id == new Guid("55555555-5555-5555-5555-222222222222"));
        Assert.Contains(segments, segment => segment.Id == new Guid("55555555-5555-5555-5555-333333333333"));
        Assert.Contains(segments, segment => segment.Id == new Guid("55555555-5555-5555-5555-444444444444"));
        Assert.Contains(segments, segment => segment.Id == new Guid("66666666-6666-6666-6666-666666666666"));
        Assert.Contains(segments, segment => segment.Id == new Guid("77777777-7777-7777-7777-777777777777"));
        Assert.Contains(segments, segment => segment.Id == new Guid("88888888-8888-8888-8888-888888888888"));
        Assert.Contains(segments, segment => segment.Id == new Guid("99999999-9999-9999-9999-999999999999"));
        Assert.Contains(segments, segment => segment.Id == new Guid("aaaaaaaa-1111-1111-1111-111111111111"));
        Assert.Contains(segments, segment => segment.Id == new Guid("bbbbbbbb-1111-1111-1111-111111111111"));
        Assert.Contains(segments, segment => segment.Id == new Guid("cccccccc-1111-1111-1111-111111111111"));
        Assert.Contains(segments, segment => segment.Id == new Guid("dddddddd-1111-1111-1111-111111111111"));
        Assert.Contains(segments, segment => segment.Id == new Guid("eeeeeeee-1111-1111-1111-111111111111"));
        Assert.Contains(segments, segment => segment.Id == new Guid("ffffffff-1111-1111-1111-111111111111"));
        Assert.Contains(segments, segment => segment.Id == new Guid("12121212-1111-1111-1111-111111111111"));
    }

    [Fact]
    public async Task GetSegment_ReturnsSegmentPlanningInfo_WithLatestFlow()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segment = await GetFirstSegment(client);

        var latestReading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segment.Id,
            ObservedAtUtc = new DateTime(2026, 4, 22, 15, 0, 0, DateTimeKind.Utc),
            EstimatedCurrentMph = 2.4,
            FlowRateCfs = 2400,
            Source = "Gauge"
        };
        flowService.AddFlowReading(latestReading);

        var response = await client.GetAsync($"/api/segments/{segment.Id}");

        response.EnsureSuccessStatusCode();
        var planning = await response.Content.ReadFromJsonAsync<SegmentPlanningApiResponse>();

        Assert.NotNull(planning);
        Assert.Equal(segment.Id, planning.Segment.Id);
        Assert.Equal(segment.PutInAddress, planning.Segment.PutInAddress);
        Assert.Equal(segment.TakeOutAddress, planning.Segment.TakeOutAddress);
        Assert.Equal(segment.PlanningSource, planning.Segment.PlanningSource);
        Assert.NotNull(planning.LatestFlow);
        Assert.Equal(latestReading.Id, planning.LatestFlow.Id);
        Assert.Equal(2.4, planning.LatestFlow.EstimatedCurrentMph);
    }

    [Fact]
    public async Task EstimateSegment_UsesLatestFlow_AndReturnsExplanation()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segment = await GetFirstSegment(client);

        flowService.AddFlowReading(new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segment.Id,
            ObservedAtUtc = new DateTime(2026, 4, 22, 15, 0, 0, DateTimeKind.Utc),
            EstimatedCurrentMph = 2.5,
            FlowRateCfs = 2500,
            Source = "Gauge"
        });

        var response = await client.PostAsJsonAsync($"/api/segments/{segment.Id}/estimate", new
        {
            paddlingSpeedMph = 5.0,
            launchTimeLocal = new DateTime(2026, 4, 22, 9, 0, 0)
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SegmentEstimateApiResponse>();

        Assert.NotNull(result);
        Assert.Equal(segment.Id, result.Segment.Id);
        Assert.NotNull(result.LatestFlow);
        Assert.Equal(2.5, result.LatestFlow.EstimatedCurrentMph);
        Assert.Equal(segment.DistanceMiles, result.Estimate.DistanceMiles);
        Assert.Equal(5.0, result.Estimate.PaddlingSpeedMph);
        Assert.Equal(2.5, result.Estimate.CurrentMphUsed);
        Assert.Equal(7.5, result.Estimate.EffectiveSpeedMph);
        Assert.Equal("flow reading", result.Estimate.CurrentSource);
        Assert.Equal("Latest available conditions", result.Estimate.ConditionBasis);
        Assert.Equal("Uses the latest stored flow reading for this run.", result.Estimate.ConditionBasisDetail);
        Assert.Contains("Base paddling speed 5 mph + current 2.5 mph", result.Estimate.ExplanationSummary);
    }

    [Fact]
    public async Task EstimateSegment_FallsBackToSegmentDefaultCurrent_WhenNoFlowExists()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segment = await GetFirstSegment(client);

        var response = await client.PostAsJsonAsync($"/api/segments/{segment.Id}/estimate", new
        {
            paddlingSpeedMph = 5.0
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SegmentEstimateApiResponse>();

        Assert.NotNull(result);
        Assert.Null(result.LatestFlow);
        Assert.Equal(segment.DefaultCurrentMph, result.Estimate.CurrentMphUsed);
        Assert.Equal(5.0 + segment.DefaultCurrentMph, result.Estimate.EffectiveSpeedMph);
        Assert.Equal("manual current", result.Estimate.CurrentSource);
        Assert.Equal("Baseline conditions", result.Estimate.ConditionBasis);
        Assert.Equal("Uses seeded or manually provided current for this run.", result.Estimate.ConditionBasisDetail);
        Assert.Contains("manual current", result.Estimate.ExplanationSummary);
    }

    [Fact]
    public async Task EstimateSegment_UsesTomorrowMorningOutlook_WhenExplicitMappedUsgsCurrentSupportsIt()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();
        var segmentId = new Guid("33333333-3333-3333-3333-333333333333");

        flowService.AddFlowReading(new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = new DateTime(2026, 4, 22, 14, 0, 0, DateTimeKind.Utc),
            EstimatedCurrentMph = 2.6,
            Source = "USGS",
            SourceReference = "02087500"
        });

        var response = await client.PostAsJsonAsync($"/api/segments/{segmentId}/estimate", new
        {
            paddlingSpeedMph = 5.0,
            launchTimeLocal = new DateTime(2026, 4, 23, 9, 0, 0)
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SegmentEstimateApiResponse>();

        Assert.NotNull(result);
        Assert.Equal(2.6, result.Estimate.CurrentMphUsed);
        Assert.Equal("tomorrow-morning outlook", result.Estimate.CurrentSource);
        Assert.Equal("Planned-launch conditions", result.Estimate.ConditionBasis);
        Assert.Equal("Uses available near-term support for the selected launch time.", result.Estimate.ConditionBasisDetail);
        Assert.Contains("tomorrow-morning outlook", result.Estimate.ExplanationSummary);
    }

    [Fact]
    public async Task EstimateSegment_ReturnsNotFound_ForUnknownSegment()
    {
        var flowService = new InMemoryFlowReadingService();
        await using var factory = CreateFactory(flowService);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/segments/{Guid.NewGuid()}/estimate", new
        {
            paddlingSpeedMph = 5.0
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private static async Task<SegmentApiResponse> GetFirstSegment(HttpClient client)
    {
        var segments = await client.GetFromJsonAsync<List<SegmentApiResponse>>("/api/segments");

        Assert.NotNull(segments);
        Assert.NotEmpty(segments);

        return segments[0];
    }

    private sealed class SegmentPlanningApiResponse
    {
        public SegmentApiResponse Segment { get; set; } = new();
        public FlowSummaryApiResponse? LatestFlow { get; set; }
    }

    private sealed class SegmentEstimateApiResponse
    {
        public SegmentApiResponse Segment { get; set; } = new();
        public FlowSummaryApiResponse? LatestFlow { get; set; }
        public TripEstimateApiResponse Estimate { get; set; } = new();
    }

    private sealed class SegmentApiResponse
    {
        public Guid Id { get; set; }
        public Guid RiverId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PutInName { get; set; } = string.Empty;
        public string TakeOutName { get; set; } = string.Empty;
        public double DistanceMiles { get; set; }
        public string? DistanceSource { get; set; }
        public double? PutInRiverMile { get; set; }
        public double? TakeOutRiverMile { get; set; }
        public string? PutInAddress { get; set; }
        public string? TakeOutAddress { get; set; }
        public string? PutInAmenities { get; set; }
        public string? TakeOutAmenities { get; set; }
        public string? PlanningSource { get; set; }
        public double? DefaultCurrentMph { get; set; }
        public bool IsActive { get; set; }
    }

    private sealed class FlowSummaryApiResponse
    {
        public Guid Id { get; set; }
        public Guid SegmentId { get; set; }
        public DateTime ObservedAtUtc { get; set; }
        public double? EstimatedCurrentMph { get; set; }
        public double? FlowRateCfs { get; set; }
        public string Source { get; set; } = string.Empty;
    }

    private sealed class TripEstimateApiResponse
    {
        public string SegmentName { get; set; } = string.Empty;
        public double DistanceMiles { get; set; }
        public double PaddlingSpeedMph { get; set; }
        public double RiverCurrentMphUsed { get; set; }
        public double CurrentMphUsed { get; set; }
        public double EffectiveSpeedMph { get; set; }
        public string Assumptions { get; set; } = string.Empty;
        public string CurrentSource { get; set; } = string.Empty;
        public string ConditionBasis { get; set; } = string.Empty;
        public string ConditionBasisDetail { get; set; } = string.Empty;
        public string ExplanationSummary { get; set; } = string.Empty;
    }
}
