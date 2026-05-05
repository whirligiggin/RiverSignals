using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class ExtractionPlanningHandoffEvaluatorTests
{
    private readonly ExtractionPlanningHandoffEvaluator _evaluator = new();

    [Fact]
    public void Evaluate_ReturnsCanEstimate_WhenRequiredPlanningInputsAreGrounded()
    {
        var segmentId = Guid.NewGuid();
        var launchTime = new DateTime(2026, 4, 23, 9, 0, 0);

        var result = _evaluator.Evaluate(new ExtractionPlanningHandoffInput
        {
            Extraction = NewExtraction(needsReview: false),
            SegmentId = segmentId,
            SegmentName = "Neuse - Anderson Point to Poole Road",
            DistanceMiles = 7.0,
            PaddlingSpeedMph = 5.0,
            RiverCurrentMphOverride = 1.5,
            LaunchTimeLocal = launchTime
        });

        Assert.True(result.CanEstimate);
        Assert.Equal(segmentId, result.SegmentId);
        Assert.Equal("Neuse - Anderson Point to Poole Road", result.SegmentName);
        Assert.Equal(7.0, result.DistanceMiles);
        Assert.Equal(5.0, result.PaddlingSpeedMph);
        Assert.Empty(result.MissingInputs);
        Assert.Empty(result.AmbiguityFlags);
        Assert.Contains($"segmentId:{segmentId}", result.AvailableInputs);
        Assert.Contains("paddlingSpeedMph:5", result.AvailableInputs);
        Assert.Equal("Planning-ready: required estimate inputs are grounded.", result.Summary);
    }

    [Fact]
    public void Evaluate_ReturnsCannotEstimate_WhenRequiredPlanningInputsAreMissing()
    {
        var result = _evaluator.Evaluate(new ExtractionPlanningHandoffInput
        {
            Extraction = new ExtractionResult
            {
                NeedsReview = false,
                RecordType = RecordType.UnclearTripRecord,
                DurationType = DurationType.Unknown
            }
        });

        Assert.False(result.CanEstimate);
        Assert.Contains("segmentId", result.MissingInputs);
        Assert.Contains("segmentName", result.MissingInputs);
        Assert.Contains("distanceMiles", result.MissingInputs);
        Assert.Contains("paddlingSpeedMph", result.MissingInputs);
        Assert.Contains("Not planning-ready", result.Summary);
    }

    [Fact]
    public void Evaluate_DoesNotResolveSegment_FromExtractedRouteText()
    {
        var result = _evaluator.Evaluate(new ExtractionPlanningHandoffInput
        {
            Extraction = new ExtractionResult
            {
                PutInLocation = "Buckhorn",
                PullOutLocation = "Lillington",
                WatercraftType = "kayak",
                DurationHours = 3.5,
                NeedsReview = false,
                RecordType = RecordType.ReportedTripResult,
                DurationType = DurationType.Actual
            },
            PaddlingSpeedMph = 5.0
        });

        Assert.False(result.CanEstimate);
        Assert.Null(result.SegmentId);
        Assert.Contains("putIn:Buckhorn", result.AvailableInputs);
        Assert.Contains("pullOut:Lillington", result.AvailableInputs);
        Assert.Contains("routeTextPresentWithoutGroundedSegment", result.AmbiguityFlags);
        Assert.Contains("segmentId", result.MissingInputs);
    }

    [Fact]
    public void Evaluate_PreservesGroundedSegmentInput_ForDownstreamPlanning()
    {
        var segmentId = Guid.NewGuid();

        var result = _evaluator.Evaluate(new ExtractionPlanningHandoffInput
        {
            Extraction = new ExtractionResult
            {
                PutInLocation = "Buckhorn",
                PullOutLocation = "Lillington",
                NeedsReview = false,
                RecordType = RecordType.ReportedTripResult,
                DurationType = DurationType.Actual
            },
            SegmentId = segmentId,
            SegmentName = "Grounded Segment",
            DistanceMiles = 8.25,
            PaddlingSpeedMph = 4.5
        });

        Assert.True(result.CanEstimate);
        Assert.Equal(segmentId, result.SegmentId);
        Assert.Equal("Grounded Segment", result.SegmentName);
        Assert.Equal(8.25, result.DistanceMiles);
        Assert.Equal(4.5, result.PaddlingSpeedMph);
        Assert.DoesNotContain("routeTextPresentWithoutGroundedSegment", result.AmbiguityFlags);
    }

    [Fact]
    public void Evaluate_PreservesExplicitContext_WhenExtractedRouteTextDiffers()
    {
        var explicitSegmentId = Guid.NewGuid();

        var result = _evaluator.Evaluate(new ExtractionPlanningHandoffInput
        {
            Extraction = new ExtractionResult
            {
                PutInLocation = "Buckhorn",
                PullOutLocation = "Lillington",
                WatercraftType = "kayak",
                NeedsReview = false,
                RecordType = RecordType.ReportedTripResult,
                DurationType = DurationType.Actual
            },
            SegmentId = explicitSegmentId,
            SegmentName = "Reviewed Neuse Segment",
            DistanceMiles = 6.75,
            PaddlingSpeedMph = 4.25
        });

        Assert.True(result.CanEstimate);
        Assert.Equal(explicitSegmentId, result.SegmentId);
        Assert.Equal("Reviewed Neuse Segment", result.SegmentName);
        Assert.Equal(6.75, result.DistanceMiles);
        Assert.Equal(4.25, result.PaddlingSpeedMph);
        Assert.Contains("putIn:Buckhorn", result.AvailableInputs);
        Assert.Contains("pullOut:Lillington", result.AvailableInputs);
        Assert.Contains($"segmentId:{explicitSegmentId}", result.AvailableInputs);
        Assert.DoesNotContain("routeTextPresentWithoutGroundedSegment", result.AmbiguityFlags);
    }

    [Fact]
    public void Evaluate_ReturnsRemainingMissingInputs_WhenExplicitContextIsPartial()
    {
        var segmentId = Guid.NewGuid();

        var result = _evaluator.Evaluate(new ExtractionPlanningHandoffInput
        {
            Extraction = new ExtractionResult
            {
                PutInLocation = "Anderson Point",
                PullOutLocation = "Poole Road",
                NeedsReview = false,
                RecordType = RecordType.ReportedTripResult,
                DurationType = DurationType.Actual
            },
            SegmentId = segmentId,
            PaddlingSpeedMph = 4.0
        });

        Assert.False(result.CanEstimate);
        Assert.Equal(segmentId, result.SegmentId);
        Assert.Contains($"segmentId:{segmentId}", result.AvailableInputs);
        Assert.Contains("paddlingSpeedMph:4", result.AvailableInputs);
        Assert.DoesNotContain("segmentId", result.MissingInputs);
        Assert.DoesNotContain("paddlingSpeedMph", result.MissingInputs);
        Assert.Contains("segmentName", result.MissingInputs);
        Assert.Contains("distanceMiles", result.MissingInputs);
        Assert.DoesNotContain("routeTextPresentWithoutGroundedSegment", result.AmbiguityFlags);
    }

    [Fact]
    public void Evaluate_UsefulExtractionFactsCanStillBeInsufficientForPlanning()
    {
        var result = _evaluator.Evaluate(new ExtractionPlanningHandoffInput
        {
            Extraction = new ExtractionResult
            {
                PutInLocation = "Anderson",
                PullOutLocation = "Poole",
                WatercraftType = "kayak",
                DurationHours = 3.0,
                TripDateOrTiming = "last weekend",
                NeedsReview = false,
                RecordType = RecordType.ReportedTripResult,
                DurationType = DurationType.Actual
            }
        });

        Assert.False(result.CanEstimate);
        Assert.Contains("putIn:Anderson", result.AvailableInputs);
        Assert.Contains("pullOut:Poole", result.AvailableInputs);
        Assert.Contains("watercraftType:kayak", result.AvailableInputs);
        Assert.Contains("durationHours:3", result.AvailableInputs);
        Assert.Contains("segmentId", result.MissingInputs);
        Assert.Contains("distanceMiles", result.MissingInputs);
        Assert.Contains("paddlingSpeedMph", result.MissingInputs);
        Assert.Contains("routeTextPresentWithoutGroundedSegment", result.AmbiguityFlags);
    }

    [Fact]
    public void Evaluate_ExtractionReviewNeedBlocksPlanningReadiness()
    {
        var result = _evaluator.Evaluate(new ExtractionPlanningHandoffInput
        {
            Extraction = NewExtraction(needsReview: true),
            SegmentId = Guid.NewGuid(),
            SegmentName = "Grounded Segment",
            DistanceMiles = 7.0,
            PaddlingSpeedMph = 5.0
        });

        Assert.False(result.CanEstimate);
        Assert.Empty(result.MissingInputs);
        Assert.Contains("extractionNeedsReview", result.AmbiguityFlags);
    }

    private static ExtractionResult NewExtraction(bool needsReview)
    {
        return new ExtractionResult
        {
            PutInLocation = "Anderson Point",
            PullOutLocation = "Poole Road",
            WatercraftType = "kayak",
            DurationHours = 2.0,
            NeedsReview = needsReview,
            RecordType = RecordType.ReportedTripResult,
            DurationType = DurationType.Actual
        };
    }
}
