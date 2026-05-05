using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class TripObservationApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task AddObservation_ReturnsStoredUserProvidedValues_ForSelectedSegment()
    {
        await using var factory = CreateFactory(new InMemoryTripObservationService());
        using var client = factory.CreateClient();
        var segmentId = new Guid("33333333-3333-3333-3333-333333333333");

        var response = await client.PostAsJsonAsync($"/api/segments/{segmentId}/observations", new
        {
            startTimeLocal = new DateTime(2026, 4, 24, 9, 0, 0),
            finishTimeLocal = new DateTime(2026, 4, 24, 11, 15, 0),
            putInText = "Used the Anderson ramp",
            takeOutText = "Took out just past the usual lot",
            notes = "Light headwind in the last mile."
        });

        response.EnsureSuccessStatusCode();
        var stored = await response.Content.ReadFromJsonAsync<TripObservationApiResponse>(JsonOptions);

        Assert.NotNull(stored);
        Assert.Equal(segmentId, stored.SegmentId);
        Assert.Equal("Neuse River - Anderson Point Park Boat Launch to Poole Road River Access", stored.SegmentName);
        Assert.Equal(ObservationReviewState.Unreviewed, stored.ReviewState);
        Assert.Equal(ObservationPipelineStage.Structured, stored.PipelineStage);
        Assert.Equal(new DateTime(2026, 4, 24, 9, 0, 0), stored.StartTimeLocal);
        Assert.Equal(new DateTime(2026, 4, 24, 11, 15, 0), stored.FinishTimeLocal);
        Assert.Null(stored.DurationMinutes);
        Assert.Equal("Used the Anderson ramp", stored.PutInText);
        Assert.Equal("Took out just past the usual lot", stored.TakeOutText);
        Assert.Equal("Light headwind in the last mile.", stored.Notes);
    }

    [Fact]
    public async Task AddObservation_AcceptsDurationWithoutFinishTime()
    {
        await using var factory = CreateFactory(new InMemoryTripObservationService());
        using var client = factory.CreateClient();
        var segmentId = new Guid("11111111-1111-1111-1111-111111111111");

        var response = await client.PostAsJsonAsync($"/api/segments/{segmentId}/observations", new
        {
            startTimeLocal = new DateTime(2026, 4, 24, 8, 30, 0),
            durationMinutes = 145
        });

        response.EnsureSuccessStatusCode();
        var stored = await response.Content.ReadFromJsonAsync<TripObservationApiResponse>(JsonOptions);

        Assert.NotNull(stored);
        Assert.Equal(145, stored.DurationMinutes);
        Assert.Null(stored.FinishTimeLocal);
        Assert.Equal(ObservationReviewState.Unreviewed, stored.ReviewState);
        Assert.Equal(ObservationPipelineStage.Structured, stored.PipelineStage);
        Assert.NotEqual(ObservationPipelineStage.Normalized, stored.PipelineStage);
        Assert.NotEqual(ObservationPipelineStage.Reviewed, stored.PipelineStage);
        Assert.NotEqual(ObservationPipelineStage.Promoted, stored.PipelineStage);
    }

    [Fact]
    public async Task AddObservation_DoesNotChangeEstimateBehavior()
    {
        await using var factory = CreateFactory(new InMemoryTripObservationService());
        using var client = factory.CreateClient();
        var segmentId = new Guid("33333333-3333-3333-3333-333333333333");
        var estimateRequest = new
        {
            paddlingSpeedMph = 3.0
        };

        var beforeResponse = await client.PostAsJsonAsync($"/api/segments/{segmentId}/estimate", estimateRequest);
        beforeResponse.EnsureSuccessStatusCode();
        var beforeEstimate = await beforeResponse.Content.ReadFromJsonAsync<SegmentEstimateApiResponse>(JsonOptions);

        var observationResponse = await client.PostAsJsonAsync($"/api/segments/{segmentId}/observations", new
        {
            startTimeLocal = new DateTime(2026, 4, 24, 9, 0, 0),
            durationMinutes = 75,
            notes = "Fast completed run, still user-provided and unreviewed."
        });
        observationResponse.EnsureSuccessStatusCode();

        var afterResponse = await client.PostAsJsonAsync($"/api/segments/{segmentId}/estimate", estimateRequest);
        afterResponse.EnsureSuccessStatusCode();
        var afterEstimate = await afterResponse.Content.ReadFromJsonAsync<SegmentEstimateApiResponse>(JsonOptions);

        Assert.NotNull(beforeEstimate);
        Assert.NotNull(afterEstimate);
        Assert.Equal(beforeEstimate.Estimate.DistanceMiles, afterEstimate.Estimate.DistanceMiles);
        Assert.Equal(beforeEstimate.Estimate.EstimatedDuration, afterEstimate.Estimate.EstimatedDuration);
        Assert.Equal(beforeEstimate.Estimate.EffectiveSpeedMph, afterEstimate.Estimate.EffectiveSpeedMph);
        Assert.Equal(beforeEstimate.Estimate.CurrentMphUsed, afterEstimate.Estimate.CurrentMphUsed);
    }

    [Fact]
    public void ObservationPipelineStage_DefinesFutureStages_WithoutAutomaticTransition()
    {
        var stages = Enum.GetValues<ObservationPipelineStage>();
        var observation = new TripObservation();

        Assert.Contains(ObservationPipelineStage.Raw, stages);
        Assert.Contains(ObservationPipelineStage.Structured, stages);
        Assert.Contains(ObservationPipelineStage.Normalized, stages);
        Assert.Contains(ObservationPipelineStage.Reviewed, stages);
        Assert.Contains(ObservationPipelineStage.Promoted, stages);
        Assert.Equal(ObservationPipelineStage.Structured, observation.PipelineStage);
        Assert.Equal(ObservationReviewState.Unreviewed, observation.ReviewState);
    }

    [Fact]
    public async Task AddObservation_ReturnsBadRequest_WhenFinishTimeAndDurationAreMissing()
    {
        await using var factory = CreateFactory(new InMemoryTripObservationService());
        using var client = factory.CreateClient();
        var segmentId = new Guid("11111111-1111-1111-1111-111111111111");

        var response = await client.PostAsJsonAsync($"/api/segments/{segmentId}/observations", new
        {
            startTimeLocal = new DateTime(2026, 4, 24, 8, 30, 0)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddObservation_ReturnsBadRequest_WhenStartTimeIsMissing()
    {
        await using var factory = CreateFactory(new InMemoryTripObservationService());
        using var client = factory.CreateClient();
        var segmentId = new Guid("11111111-1111-1111-1111-111111111111");

        var response = await client.PostAsJsonAsync($"/api/segments/{segmentId}/observations", new
        {
            durationMinutes = 90
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(ITripObservationService tripObservationService)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        service => service.ServiceType == typeof(ITripObservationService));

                    if (descriptor != null)
                        services.Remove(descriptor);

                    services.AddSingleton(tripObservationService);
                });
            });
    }

    private sealed class TripObservationApiResponse
    {
        public Guid Id { get; set; }
        public Guid SegmentId { get; set; }
        public string SegmentName { get; set; } = string.Empty;
        public ObservationReviewState ReviewState { get; set; }
        public ObservationPipelineStage PipelineStage { get; set; }
        public DateTime StartTimeLocal { get; set; }
        public DateTime? FinishTimeLocal { get; set; }
        public int? DurationMinutes { get; set; }
        public string? PutInText { get; set; }
        public string? TakeOutText { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class SegmentEstimateApiResponse
    {
        public TripEstimate Estimate { get; set; } = new();
    }
}
