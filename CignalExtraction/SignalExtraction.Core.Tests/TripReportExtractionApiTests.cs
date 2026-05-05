using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Tests;

public class TripReportExtractionApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task ExtractTripReport_ReturnsStructuredReviewableFields_ForMessyReport()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        const string report = "We put in at Buckhorn around 9 and took out at Lillington around sundown. " +
            "Two kayaks, took about 3 hours 30 minutes. Low water in a few spots, had to walk and pull the boat.";

        var response = await client.PostAsJsonAsync("/api/trip-reports/extract", new
        {
            text = report,
            sourceType = "ManualPaste",
            communicationDateTime = new DateTime(2026, 4, 22, 18, 0, 0)
        });

        response.EnsureSuccessStatusCode();
        var extracted = await response.Content.ReadFromJsonAsync<TripReportExtractionApiResponse>(JsonOptions);

        Assert.NotNull(extracted);
        Assert.Equal(report, extracted.SourceText);
        Assert.Equal("Buckhorn", extracted.PutIn);
        Assert.Equal("Lillington", extracted.PullOut);
        Assert.Equal("kayak", extracted.WatercraftType);
        Assert.Equal(3.5, extracted.DurationHours);
        Assert.Equal("3 hours 30 minutes", extracted.DurationText);
        Assert.Equal(DurationType.Actual, extracted.DurationType);
        Assert.Equal(RecordType.ReportedTripResult, extracted.RecordType);
        Assert.Contains("sundown", extracted.TripDateOrTiming, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("walked and pulled boat", extracted.ConditionsOrNotes, StringComparison.OrdinalIgnoreCase);
        Assert.True(extracted.Confidence > 0.60);
        Assert.False(extracted.NeedsReview);

        Assert.Contains("put in at Buckhorn", extracted.PutInSourceText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("took out at Lillington", extracted.PullOutSourceText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("kayaks", extracted.WatercraftSourceText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sundown", extracted.TripDateOrTimingSourceText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractTripReport_PreservesAmbiguousInput_ForReview()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        const string report = "Probably around 4 hours maybe, depending on flow.";

        var response = await client.PostAsJsonAsync("/api/trip-reports/extract", new
        {
            text = report
        });

        response.EnsureSuccessStatusCode();
        var extracted = await response.Content.ReadFromJsonAsync<TripReportExtractionApiResponse>(JsonOptions);

        Assert.NotNull(extracted);
        Assert.Equal(report, extracted.SourceText);
        Assert.Null(extracted.PutIn);
        Assert.Null(extracted.PullOut);
        Assert.Null(extracted.WatercraftType);
        Assert.Equal(4.0, extracted.DurationHours);
        Assert.Equal("4 hours", extracted.DurationText);
        Assert.Equal(RecordType.UnclearTripRecord, extracted.RecordType);
        Assert.Equal(DurationType.Unknown, extracted.DurationType);
        Assert.True(extracted.NeedsReview);
        Assert.True(extracted.Confidence < 0.60);
        Assert.Empty(extracted.ReviewFlags);
    }

    [Fact]
    public async Task ExtractTripReport_DefaultsSourceTypeToManualPaste()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/trip-reports/extract", new
        {
            text = "We did Anderson to Poole last weekend in kayaks. Took about 3 hours."
        });

        response.EnsureSuccessStatusCode();
        var extracted = await response.Content.ReadFromJsonAsync<TripReportExtractionApiResponse>(JsonOptions);

        Assert.NotNull(extracted);
        Assert.Equal(SourceType.ManualPaste, extracted.SourceType);
    }

    [Fact]
    public async Task ExtractTripReport_ReturnsBadRequest_WhenTextIsMissing()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/trip-reports/extract", new
        {
            text = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PlanningHandoff_ReturnsCannotEstimate_ForExtractionOnlyInput()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/trip-reports/planning-handoff", new
        {
            extraction = new
            {
                putIn = "Anderson Point",
                pullOut = "Poole Road",
                watercraftType = "kayak",
                durationHours = 2.0,
                recordType = "ReportedTripResult",
                durationType = "Actual",
                sourceType = "ManualPaste",
                needsReview = false
            }
        });

        response.EnsureSuccessStatusCode();
        var handoff = await response.Content.ReadFromJsonAsync<PlanningHandoffApiResponse>(JsonOptions);

        Assert.NotNull(handoff);
        Assert.False(handoff.CanEstimate);
        Assert.Null(handoff.SegmentId);
        Assert.Contains("putIn:Anderson Point", handoff.AvailableInputs);
        Assert.Contains("pullOut:Poole Road", handoff.AvailableInputs);
        Assert.Contains("segmentId", handoff.MissingInputs);
        Assert.Contains("segmentName", handoff.MissingInputs);
        Assert.Contains("distanceMiles", handoff.MissingInputs);
        Assert.Contains("paddlingSpeedMph", handoff.MissingInputs);
        Assert.Contains("routeTextPresentWithoutGroundedSegment", handoff.AmbiguityFlags);
    }

    [Fact]
    public async Task PlanningHandoff_ReturnsCanEstimate_WhenExplicitContextIsSufficient()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var segmentId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/trip-reports/planning-handoff", new
        {
            extraction = new
            {
                putIn = "Anderson Point",
                pullOut = "Poole Road",
                recordType = "ReportedTripResult",
                durationType = "Actual",
                sourceType = "ManualPaste",
                needsReview = false
            },
            segmentId,
            segmentName = "Reviewed Neuse Segment",
            distanceMiles = 7.25,
            paddlingSpeedMph = 4.5
        });

        response.EnsureSuccessStatusCode();
        var handoff = await response.Content.ReadFromJsonAsync<PlanningHandoffApiResponse>(JsonOptions);

        Assert.NotNull(handoff);
        Assert.True(handoff.CanEstimate);
        Assert.Equal(segmentId, handoff.SegmentId);
        Assert.Equal("Reviewed Neuse Segment", handoff.SegmentName);
        Assert.Equal(7.25, handoff.DistanceMiles);
        Assert.Equal(4.5, handoff.PaddlingSpeedMph);
        Assert.Empty(handoff.MissingInputs);
        Assert.Empty(handoff.AmbiguityFlags);
        Assert.Contains($"segmentId:{segmentId}", handoff.AvailableInputs);
        Assert.Contains("putIn:Anderson Point", handoff.AvailableInputs);
    }

    [Fact]
    public async Task PlanningHandoff_DoesNotPromoteRouteTextToSegmentIdentity()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/trip-reports/planning-handoff", new
        {
            extraction = new
            {
                putIn = "Buckhorn",
                pullOut = "Lillington",
                recordType = "ReportedTripResult",
                durationType = "Actual",
                sourceType = "ManualPaste",
                needsReview = false
            },
            paddlingSpeedMph = 4.0
        });

        response.EnsureSuccessStatusCode();
        var handoff = await response.Content.ReadFromJsonAsync<PlanningHandoffApiResponse>(JsonOptions);

        Assert.NotNull(handoff);
        Assert.False(handoff.CanEstimate);
        Assert.Null(handoff.SegmentId);
        Assert.Contains("putIn:Buckhorn", handoff.AvailableInputs);
        Assert.Contains("pullOut:Lillington", handoff.AvailableInputs);
        Assert.Contains("paddlingSpeedMph:4", handoff.AvailableInputs);
        Assert.Contains("segmentId", handoff.MissingInputs);
        Assert.Contains("routeTextPresentWithoutGroundedSegment", handoff.AmbiguityFlags);
    }

    [Fact]
    public async Task PlanningHandoff_ReturnsMissingInputs_WhenExplicitContextIsPartial()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var segmentId = Guid.NewGuid();

        var response = await client.PostAsJsonAsync("/api/trip-reports/planning-handoff", new
        {
            extraction = new
            {
                putIn = "Anderson Point",
                pullOut = "Poole Road",
                recordType = "ReportedTripResult",
                durationType = "Actual",
                sourceType = "ManualPaste",
                needsReview = false
            },
            segmentId,
            paddlingSpeedMph = 4.0
        });

        response.EnsureSuccessStatusCode();
        var handoff = await response.Content.ReadFromJsonAsync<PlanningHandoffApiResponse>(JsonOptions);

        Assert.NotNull(handoff);
        Assert.False(handoff.CanEstimate);
        Assert.Equal(segmentId, handoff.SegmentId);
        Assert.DoesNotContain("segmentId", handoff.MissingInputs);
        Assert.DoesNotContain("paddlingSpeedMph", handoff.MissingInputs);
        Assert.Contains("segmentName", handoff.MissingInputs);
        Assert.Contains("distanceMiles", handoff.MissingInputs);
        Assert.DoesNotContain("routeTextPresentWithoutGroundedSegment", handoff.AmbiguityFlags);
    }

    [Fact]
    public async Task ExistingExtractEndpoint_RemainsAvailable()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/extract", new
        {
            text = "We did Anderson to Poole last weekend in kayaks. Took about 3 hours.",
            sourceType = "ManualPaste"
        });

        response.EnsureSuccessStatusCode();
        var extracted = await response.Content.ReadFromJsonAsync<ExistingExtractionApiResponse>(JsonOptions);

        Assert.NotNull(extracted);
        Assert.Equal("Anderson", extracted.PutInLocation);
        Assert.Equal("Poole", extracted.PullOutLocation);
    }

    private sealed class TripReportExtractionApiResponse
    {
        public string SourceText { get; set; } = string.Empty;
        public RecordType RecordType { get; set; }
        public string? PutIn { get; set; }
        public string? PutInSourceText { get; set; }
        public string? PullOut { get; set; }
        public string? PullOutSourceText { get; set; }
        public string? WatercraftType { get; set; }
        public string? WatercraftSourceText { get; set; }
        public double? DurationHours { get; set; }
        public string? DurationText { get; set; }
        public DurationType DurationType { get; set; }
        public string? TripDateOrTiming { get; set; }
        public string? TripDateOrTimingSourceText { get; set; }
        public string? ConditionsOrNotes { get; set; }
        public SourceType SourceType { get; set; }
        public DateTime? CommunicationDateTime { get; set; }
        public double Confidence { get; set; }
        public bool NeedsReview { get; set; }
        public List<string> ReviewFlags { get; set; } = new();
    }

    private sealed class PlanningHandoffApiResponse
    {
        public bool CanEstimate { get; set; }
        public Guid? SegmentId { get; set; }
        public string? SegmentName { get; set; }
        public double? DistanceMiles { get; set; }
        public double? PaddlingSpeedMph { get; set; }
        public double? RiverCurrentMphOverride { get; set; }
        public DateTime? LaunchTimeLocal { get; set; }
        public List<string> AvailableInputs { get; set; } = new();
        public List<string> MissingInputs { get; set; } = new();
        public List<string> AmbiguityFlags { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
    }

    private sealed class ExistingExtractionApiResponse
    {
        public string? PutInLocation { get; set; }
        public string? PullOutLocation { get; set; }
    }
}
