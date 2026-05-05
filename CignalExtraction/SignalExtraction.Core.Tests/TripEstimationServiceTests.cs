using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class TripEstimationServiceTests
{
    /// <summary>
    /// Tests that trip estimation uses flow reading when available.
    /// </summary>
    [Fact]
    public void Estimate_UsesFlowReading_WhenAvailable()
    {
        // Arrange
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();
        
        // Add a flow reading with 2.5 mph current
        var flowReading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow.AddHours(-1),
            EstimatedCurrentMph = 2.5,
            Source = "Test",
            GaugeHeightFeet = 5.0,
            FlowRateCfs = 1500
        };
        flowService.AddFlowReading(flowReading);

        var service = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 1.0, // Manual override (should be ignored in favor of flow)
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var result = service.Estimate(request);

        // Assert
        Assert.Equal(2.5, result.RiverCurrentMphUsed);
        Assert.Equal(2.5, result.CurrentMphUsed);
        Assert.Equal(7.5, result.EffectiveSpeedMph);
        Assert.Contains("flow reading", result.Assumptions, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("flow reading", result.CurrentSource);
        Assert.Equal("Latest available conditions", result.ConditionBasis);
        Assert.Equal("Uses the latest stored flow reading for this run.", result.ConditionBasisDetail);
        Assert.Contains("Base paddling speed 5 mph + current 2.5 mph", result.ExplanationSummary);
        Assert.Contains("effective 7.5 mph", result.ExplanationSummary);
    }

    /// <summary>
    /// Tests that trip estimation falls back to manual input when no flow reading exists.
    /// </summary>
    [Fact]
    public void Estimate_FallsBackToManualInput_WhenNoFlowReadingAvailable()
    {
        // Arrange
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();
        // No flow readings added

        var service = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 1.5, // Manual current
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var result = service.Estimate(request);

        // Assert
        Assert.Equal(1.5, result.RiverCurrentMphUsed);
        Assert.Equal(1.5, result.CurrentMphUsed);
        Assert.Equal(6.5, result.EffectiveSpeedMph);
        Assert.Contains("manually entered", result.Assumptions, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("manual current", result.CurrentSource);
        Assert.Equal("Baseline conditions", result.ConditionBasis);
        Assert.Equal("Uses seeded or manually provided current for this run.", result.ConditionBasisDetail);
        Assert.Contains("manual current", result.ExplanationSummary);
    }

    /// <summary>
    /// Tests that trip estimation works without a flow service (backward compatibility).
    /// </summary>
    [Fact]
    public void Estimate_WorksWithoutFlowService_ForBackwardCompatibility()
    {
        // Arrange
        var service = new TripEstimationService(); // No flow service
        
        var request = new TripEstimateRequest
        {
            SegmentId = Guid.NewGuid(),
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 2.0,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var result = service.Estimate(request);

        // Assert
        Assert.Equal(2.0, result.RiverCurrentMphUsed);
        Assert.Equal(2.0, result.CurrentMphUsed);
        Assert.Equal(7.0, result.EffectiveSpeedMph);
        Assert.Contains("manually entered", result.Assumptions, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("manual current", result.CurrentSource);
        Assert.Contains("effective 7 mph", result.ExplanationSummary);
    }

    /// <summary>
    /// Tests that validation still rejects invalid effective speeds.
    /// </summary>
    [Fact]
    public void Estimate_ThrowsArgumentException_WhenEffectiveSpeedIsZeroOrNegative()
    {
        // Arrange
        var service = new TripEstimationService();

        var request = new TripEstimateRequest
        {
            SegmentId = Guid.NewGuid(),
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 2,
            RiverCurrentMphOverride = -3, // Results in negative effective speed
            LaunchTimeLocal = DateTime.Now
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => service.Estimate(request));
        Assert.Contains("Effective speed must be greater than zero", ex.Message);
    }

    /// <summary>
    /// Tests that flow reading is preferred over manual input when both are available.
    /// </summary>
    [Fact]
    public void Estimate_PrefersFlowReading_OverManualInput_WhenBothAvailable()
    {
        // Arrange
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();

        var flowReading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow,
            EstimatedCurrentMph = 3.0,
            Source = "Test"
        };
        flowService.AddFlowReading(flowReading);

        var service = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 1.0, // Manual input
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var result = service.Estimate(request);

        // Assert
        Assert.Equal(3.0, result.RiverCurrentMphUsed); // Uses flow, not manual
        Assert.NotEqual(1.0, result.RiverCurrentMphUsed);
        Assert.Equal("flow reading", result.CurrentSource);
        Assert.Contains("flow reading", result.ExplanationSummary);
    }

    /// <summary>
    /// Tests that multiple flow readings are ordered by timestamp (latest used).
    /// </summary>
    [Fact]
    public void Estimate_UsesLatestFlowReading_WhenMultipleExist()
    {
        // Arrange
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();

        // Add older reading
        flowService.AddFlowReading(new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow.AddHours(-2),
            EstimatedCurrentMph = 1.0,
            Source = "Old"
        });

        // Add newer reading
        flowService.AddFlowReading(new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow,
            EstimatedCurrentMph = 2.5,
            Source = "New"
        });

        var service = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 1.5,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var result = service.Estimate(request);

        // Assert
        Assert.Equal(2.5, result.RiverCurrentMphUsed); // Uses newer reading
        Assert.Equal(2.5, result.CurrentMphUsed);
        Assert.Equal("flow reading", result.CurrentSource);
    }

    /// <summary>
    /// Tests that zero river current is accepted when explicit.
    /// </summary>
    [Fact]
    public void Estimate_AcceptsZeroCurrent_WhenExplicitlySet()
    {
        // Arrange
        var service = new TripEstimationService();

        var request = new TripEstimateRequest
        {
            SegmentId = Guid.NewGuid(),
            SegmentName = "Still Water Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 0,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var result = service.Estimate(request);

        // Assert
        Assert.Equal(0, result.RiverCurrentMphUsed);
        Assert.Equal(0, result.CurrentMphUsed);
        Assert.Equal(5, result.EffectiveSpeedMph);
        Assert.Equal("manual current", result.CurrentSource);
        Assert.Contains("current 0 mph", result.ExplanationSummary);
    }

    /// <summary>
    /// Tests that explanation distinguishes implicit zero current from manual current.
    /// </summary>
    [Fact]
    public void Estimate_ExplainsDefaultZeroCurrent_WhenNoFlowOrManualCurrent()
    {
        // Arrange
        var service = new TripEstimationService();

        var request = new TripEstimateRequest
        {
            SegmentId = Guid.NewGuid(),
            SegmentName = "No Current Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var result = service.Estimate(request);

        // Assert
        Assert.Equal(0, result.RiverCurrentMphUsed);
        Assert.Equal(0, result.CurrentMphUsed);
        Assert.Equal(5, result.EffectiveSpeedMph);
        Assert.Equal("default zero current", result.CurrentSource);
        Assert.Contains("default zero current", result.ExplanationSummary);
    }

    /// <summary>
    /// Tests that FormatDuration produces correct string representation.
    /// </summary>
    [Fact]
    public void FormatDuration_ProducesCorrectOutput()
    {
        // Arrange
        var service = new TripEstimationService();
        var duration = TimeSpan.FromHours(3.5);

        // Act
        var result = service.FormatDuration(duration);

        // Assert
        Assert.Equal("3 hr 30 min", result);
    }

    /// <summary>
    /// Tests that estimated finish time is calculated correctly when launch time is provided.
    /// </summary>
    [Fact]
    public void Estimate_CalculatesFinishTime_WhenLaunchTimeProvided()
    {
        // Arrange
        var service = new TripEstimationService();
        var launchTime = new DateTime(2026, 4, 21, 9, 0, 0);

        var request = new TripEstimateRequest
        {
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 0,
            LaunchTimeLocal = launchTime
        };

        // Act
        var result = service.Estimate(request);

        // Assert
        Assert.NotNull(result.EstimatedFinishTimeLocal);
        Assert.Equal(TimeSpan.FromHours(2), result.EstimatedDuration);
        Assert.Equal(launchTime.AddHours(2), result.EstimatedFinishTimeLocal);
        Assert.Equal("manual current", result.CurrentSource);
        Assert.Contains("Base paddling speed 5 mph + current 0 mph", result.ExplanationSummary);
    }

    [Fact]
    public void Estimate_UsesTomorrowMorningOutlook_WhenAvailable()
    {
        var segmentId = new Guid("33333333-3333-3333-3333-333333333333");
        var flowService = new InMemoryFlowReadingService();
        var observedAtUtc = new DateTime(2026, 4, 22, 14, 0, 0, DateTimeKind.Utc);

        flowService.AddFlowReading(new FlowReading
        {
            Id = new Guid("aaaaaaaa-0000-0000-0000-000000000002"),
            SegmentId = segmentId,
            ObservedAtUtc = observedAtUtc,
            EstimatedCurrentMph = 2.4,
            Source = "USGS"
        });

        var outlookService = new NearTermFlowOutlookService(new SegmentCatalogService(), flowService);
        var service = new TripEstimationService(flowService, outlookService);

        var result = service.Estimate(new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Neuse River - Anderson Point Park Boat Launch to Poole Road River Access",
            DistanceMiles = 7,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 1.5,
            LaunchTimeLocal = new DateTime(2026, 4, 23, 9, 0, 0)
        });

        Assert.Equal(2.4, result.CurrentMphUsed);
        Assert.Equal("tomorrow-morning outlook", result.CurrentSource);
        Assert.Equal("Planned-launch conditions", result.ConditionBasis);
        Assert.Equal("Uses available near-term support for the selected launch time.", result.ConditionBasisDetail);
        Assert.Contains("tomorrow-morning outlook", result.ExplanationSummary);
        Assert.Contains("bounded tomorrow-morning outlook", result.Assumptions);
        Assert.Equal(new Guid("aaaaaaaa-0000-0000-0000-000000000002"), result.UsedFlowReadingId);
        Assert.Equal(observedAtUtc, result.UsedFlowReadingTimestamp);
        Assert.Equal("USGS", result.UsedFlowReadingSource);
    }
}
