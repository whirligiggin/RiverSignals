using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class UsgsFlowImportApiTests
{
    [Fact]
    public async Task ImportCurrent_StoresNormalizedFlowReadings_ForMappedSegments()
    {
        var flowService = new InMemoryFlowReadingService();
        var fakeClient = new FakeUsgsInstantaneousValuesClient(
        [
            new UsgsGaugeInstantaneousReading
            {
                GaugeId = new Guid("30111111-1111-1111-1111-111111111111"),
                StationId = "02087183",
                GaugeName = "Neuse River | Falls Dam (Raleigh)",
                ObservedAtUtc = new DateTime(2026, 4, 27, 17, 45, 0, DateTimeKind.Utc),
                GaugeHeightFeet = 4.82,
                FlowRateCfs = 3120
            },
            new UsgsGaugeInstantaneousReading
            {
                GaugeId = new Guid("30777777-7777-7777-7777-111111111111"),
                StationId = "02096500",
                GaugeName = "Haw River | near Bynum",
                ObservedAtUtc = new DateTime(2026, 4, 27, 17, 30, 0, DateTimeKind.Utc),
                GaugeHeightFeet = 6.10,
                FlowRateCfs = 2550
            }
        ]);

        await using var factory = CreateFactory(flowService, fakeClient);
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/api/flow/import/usgs/current", content: null);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<UsgsFlowImportApiResponse>();
        Assert.NotNull(payload);
        Assert.Equal(10, payload.GaugesQueried);
        Assert.Equal(2, payload.GaugeReadingsReceived);
        Assert.Equal(5, payload.FlowReadingsStored);

        var importedSegmentIds = payload.ImportedReadings.Select(reading => reading.SegmentId).ToHashSet();
        Assert.Contains(new Guid("55555555-5555-5555-5555-555555555555"), importedSegmentIds);
        Assert.Contains(new Guid("66666666-6666-6666-6666-666666666666"), importedSegmentIds);
        Assert.Contains(new Guid("77777777-7777-7777-7777-777777777777"), importedSegmentIds);
        Assert.Contains(new Guid("11111111-1111-1111-1111-111111111111"), importedSegmentIds);
        Assert.Contains(new Guid("dddddddd-1111-1111-1111-111111111111"), importedSegmentIds);

        var latestNeuseReading = flowService.GetLatestForSegment(new Guid("55555555-5555-5555-5555-555555555555"));
        Assert.NotNull(latestNeuseReading);
        Assert.Equal(new DateTime(2026, 4, 27, 17, 45, 0, DateTimeKind.Utc), latestNeuseReading.ObservedAtUtc);
        Assert.Null(latestNeuseReading.EstimatedCurrentMph);
        Assert.Equal(4.82, latestNeuseReading.GaugeHeightFeet);
        Assert.Equal(3120, latestNeuseReading.FlowRateCfs);
        Assert.Equal("USGS", latestNeuseReading.Source);
        Assert.Equal("02087183", latestNeuseReading.SourceReference);
    }

    [Fact]
    public async Task ImportCurrent_MakesImportedReadingVisible_ToExistingPlanningFlow()
    {
        var flowService = new InMemoryFlowReadingService();
        var fakeClient = new FakeUsgsInstantaneousValuesClient(
        [
            new UsgsGaugeInstantaneousReading
            {
                GaugeId = new Guid("30111111-1111-1111-1111-222222222222"),
                StationId = "02087500",
                GaugeName = "Neuse River | Clayton",
                ObservedAtUtc = new DateTime(2026, 4, 27, 18, 00, 0, DateTimeKind.Utc),
                GaugeHeightFeet = 5.25,
                FlowRateCfs = 4100
            }
        ]);

        await using var factory = CreateFactory(flowService, fakeClient);
        using var client = factory.CreateClient();

        var importResponse = await client.PostAsync("/api/flow/import/usgs/current", content: null);
        importResponse.EnsureSuccessStatusCode();

        var segmentResponse = await client.GetFromJsonAsync<SegmentPlanningApiResponse>(
            "/api/segments/33333333-3333-3333-3333-333333333333");

        Assert.NotNull(segmentResponse);
        Assert.NotNull(segmentResponse.LatestFlow);
        Assert.Equal("USGS", segmentResponse.LatestFlow.Source);
        Assert.Equal(4100, segmentResponse.LatestFlow.FlowRateCfs);
        Assert.Equal(new DateTime(2026, 4, 27, 18, 00, 0, DateTimeKind.Utc), segmentResponse.LatestFlow.ObservedAtUtc);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        IFlowReadingService flowService,
        IUsgsInstantaneousValuesClient usgsClient)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    ReplaceSingleton(services, flowService);
                    ReplaceScoped(services, usgsClient);
                });
            });
    }

    private static void ReplaceSingleton(IServiceCollection services, IFlowReadingService flowService)
    {
        var descriptor = services.SingleOrDefault(service => service.ServiceType == typeof(IFlowReadingService));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddSingleton(flowService);
    }

    private static void ReplaceScoped(IServiceCollection services, IUsgsInstantaneousValuesClient usgsClient)
    {
        var descriptor = services.SingleOrDefault(service => service.ServiceType == typeof(IUsgsInstantaneousValuesClient));
        if (descriptor != null)
            services.Remove(descriptor);

        services.AddScoped(_ => usgsClient);
    }

    private sealed class FakeUsgsInstantaneousValuesClient : IUsgsInstantaneousValuesClient
    {
        private readonly IReadOnlyList<UsgsGaugeInstantaneousReading> _readings;

        public FakeUsgsInstantaneousValuesClient(IReadOnlyList<UsgsGaugeInstantaneousReading> readings)
        {
            _readings = readings;
        }

        public Task<IReadOnlyList<UsgsGaugeInstantaneousReading>> GetCurrentReadingsAsync(
            IEnumerable<UsgsGauge> gauges,
            CancellationToken cancellationToken = default)
        {
            var allowedGaugeIds = gauges.Select(gauge => gauge.Id).ToHashSet();
            var filtered = _readings.Where(reading => allowedGaugeIds.Contains(reading.GaugeId)).ToList();
            return Task.FromResult<IReadOnlyList<UsgsGaugeInstantaneousReading>>(filtered);
        }
    }

    private sealed class UsgsFlowImportApiResponse
    {
        public int GaugesQueried { get; set; }
        public int GaugeReadingsReceived { get; set; }
        public int FlowReadingsStored { get; set; }
        public List<ImportedFlowReadingApiResponse> ImportedReadings { get; set; } = [];
    }

    private sealed class ImportedFlowReadingApiResponse
    {
        public Guid SegmentId { get; set; }
        public Guid GaugeId { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string GaugeName { get; set; } = string.Empty;
        public DateTime ObservedAtUtc { get; set; }
        public double? GaugeHeightFeet { get; set; }
        public double? FlowRateCfs { get; set; }
    }

    private sealed class SegmentPlanningApiResponse
    {
        public SegmentFlowSummaryApiResponse? LatestFlow { get; set; }
    }

    private sealed class SegmentFlowSummaryApiResponse
    {
        public DateTime ObservedAtUtc { get; set; }
        public double? FlowRateCfs { get; set; }
        public string Source { get; set; } = string.Empty;
    }
}
