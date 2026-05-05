using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class ExtractionServiceTests
{
    private readonly ExtractionService _service = new();

    [Fact]
    public async Task ExtractAsync_UsesAnchoredLocations_BeforeGenericRouteParsing()
    {
        var request = new ExtractionRequest
        {
            Text = "We took out at Lillington around sundown. We started at Buckhorn Dam.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.Equal("Buckhorn Dam", result.PutInLocation);
        Assert.Equal("Lillington", result.PullOutLocation);
        Assert.Equal("started at Buckhorn Dam", result.PutInSourceText);
        Assert.Equal("took out at Lillington", result.PullOutSourceText);
    }

    [Fact]
    public async Task ExtractAsync_FallsBackToRouteParsing_WhenAnchorsAreAbsent()
    {
        var request = new ExtractionRequest
        {
            Text = "We did Anderson to Poole last weekend in kayaks.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.Equal("Anderson", result.PutInLocation);
        Assert.Equal("Poole", result.PullOutLocation);
    }

    [Fact]
    public async Task ExtractAsync_DoesNotPromoteSentenceFragments_AsLocations()
    {
        var request = new ExtractionRequest
        {
            Text = "We took out at Lillington around sundown. We started at Buckhorn dam. We dragged bottom often and had to walk and pull the boat through a few times.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.False(string.Equals("we dragged bottom often and had", result.PutInLocation, StringComparison.OrdinalIgnoreCase));
        Assert.False(string.Equals("walk", result.PullOutLocation, StringComparison.OrdinalIgnoreCase));

        Assert.Equal("Buckhorn dam", result.PutInLocation, ignoreCase: true);
        Assert.Equal("Lillington", result.PullOutLocation, ignoreCase: true);
    }

    [Fact]
    public async Task ExtractAsync_NormalizesRangeDuration_ByAveraging()
    {
        var request = new ExtractionRequest
        {
            Text = "We did Anderson to Poole last weekend in kayaks. Took about 3 to 3.5 hours.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.NotNull(result.DurationHours);
        Assert.Equal(3.25, result.DurationHours!.Value, precision: 2);
        Assert.Contains("3", result.DurationSourceText);
        Assert.Contains("3.5", result.DurationSourceText);
    }

    [Fact]
    public async Task ExtractAsync_NormalizesHyphenRangeDuration_ByAveraging()
    {
        var request = new ExtractionRequest
        {
            Text = "We did Anderson to Poole last weekend in kayaks. Took about 3-3.5 hours.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.NotNull(result.DurationHours);
        Assert.Equal(3.25, result.DurationHours!.Value, precision: 2);
        Assert.Contains("3-3.5", result.DurationSourceText);
    }

    [Fact]
    public async Task ExtractAsync_NormalizesBetweenRangeDuration_ByAveraging()
    {
        var request = new ExtractionRequest
        {
            Text = "We did Anderson to Poole last weekend in kayaks. It took between 3 and 4 hours.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.NotNull(result.DurationHours);
        Assert.Equal(3.5, result.DurationHours!.Value, precision: 2);
        Assert.Contains("between 3 and 4 hours", result.DurationSourceText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsHoursAndMinutesDuration()
    {
        var request = new ExtractionRequest
        {
            Text = "We did Anderson to Poole last weekend in kayaks. Took about 3 hours 30 minutes.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.NotNull(result.DurationHours);
        Assert.Equal(3.5, result.DurationHours!.Value, precision: 2);
        Assert.Contains("3 hours 30 minutes", result.DurationSourceText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_ClassifiesReportedTripResult_FromPastTenseTripNarrative()
    {
        var request = new ExtractionRequest
        {
            Text = "We did Anderson to Poole last weekend in kayaks. Took about 3 hours.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.Equal(RecordType.ReportedTripResult, result.RecordType);
        Assert.Equal(DurationType.Actual, result.DurationType);
    }

    [Fact]
    public async Task ExtractAsync_ClassifiesEstimate_FromAdvisoryLanguage()
    {
        var request = new ExtractionRequest
        {
            Text = "Plan on about 4 hours from Buckhorn Dam to Lillington depending on flow.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.Equal(RecordType.TripEstimate, result.RecordType);
        Assert.Equal(DurationType.Estimate, result.DurationType);
    }

    [Fact]
    public async Task ExtractAsync_ExtractsStrongTimingPhrase_WhenPresent()
    {
        var request = new ExtractionRequest
        {
            Text = "We took out at Lillington around sundown after starting at Buckhorn Dam.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.NotNull(result.TripDateOrTiming);
        Assert.Contains("sundown", result.TripDateOrTiming!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExtractAsync_CapturesWatercraft_WhenExplicitlyMentioned()
    {
        var request = new ExtractionRequest
        {
            Text = "We did Anderson to Poole last weekend in two kayaks.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.Equal("kayak", result.WatercraftType);
    }

    [Fact]
    public async Task ExtractAsync_PreservesConditionsAndNotes_ForRelevantTripContext()
    {
        var request = new ExtractionRequest
        {
            Text = "We took out at Lillington around sundown. We started at Buckhorn Dam. We dragged bottom often and had to walk and pull the boat through a few times. Between Raven Rock and Lillington the water goes slack and slow.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.NotNull(result.ConditionsOrNotes);
    }

    [Fact]
    public async Task ExtractAsync_FlagsReview_WhenImportantDataIsMissingOrAmbiguous()
    {
        var request = new ExtractionRequest
        {
            Text = "Plan on about 4 hours depending on flow.",
            SourceType = SourceType.ManualPaste
        };

        var result = await _service.ExtractAsync(request);

        Assert.True(result.NeedsReview);
    }

    [Fact]
    public async Task ExtractAsync_ConfidenceIsHigher_ForCoherentRichExtractions()
    {
        var strongRequest = new ExtractionRequest
        {
            Text = "We did Anderson to Poole last weekend in kayaks. Took about 3 hours.",
            SourceType = SourceType.ManualPaste
        };

        var weakRequest = new ExtractionRequest
        {
            Text = "Probably around 4 hours maybe.",
            SourceType = SourceType.ManualPaste
        };

        var strongResult = await _service.ExtractAsync(strongRequest);
        var weakResult = await _service.ExtractAsync(weakRequest);

        Assert.True(strongResult.ExtractionConfidence > weakResult.ExtractionConfidence);
    }
}