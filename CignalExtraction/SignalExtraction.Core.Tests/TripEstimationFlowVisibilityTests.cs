using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

/// <summary>
/// Tests for flow data ingestion and visibility in trip estimates.
/// Verifies that FlowReading data can be added to the system and that
/// TripEstimate output includes traceability information.
/// </summary>
public class TripEstimationFlowVisibilityTests
{
    /// <summary>
    /// Tests the flow ingestion path: AddFlowReading stores data.
    /// </summary>
    [Fact]
    public void FlowIngestion_AddFlowReading_StoresDataForRetrieval()
    {
        // Arrange
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();

        var reading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow,
            EstimatedCurrentMph = 2.3,
            Source = "TestGauge"
        };

        // Act
        flowService.AddFlowReading(reading);
        var retrieved = flowService.GetLatestForSegment(segmentId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(reading.Id, retrieved.Id);
        Assert.Equal(2.3, retrieved.EstimatedCurrentMph);
    }

    /// <summary>
    /// Tests that TripEstimate includes flow traceability fields when flow data is used.
    /// </summary>
    [Fact]
    public void FlowVisibility_Estimate_IncludesFlowTraceability_WhenFlowUsed()
    {
        // Arrange
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();

        var flowReading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = new DateTime(2026, 4, 22, 14, 30, 0),
            EstimatedCurrentMph = 2.8,
            Source = "USGSGauge",
            SourceReference = "Station_12345"
        };
        flowService.AddFlowReading(flowReading);

        var estimationService = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 1.0, // Manual override (ignored due to flow)
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var estimate = estimationService.Estimate(request);

        // Assert: Traceability fields populated
        Assert.Equal(flowReading.Id, estimate.UsedFlowReadingId);
        Assert.Equal(flowReading.ObservedAtUtc, estimate.UsedFlowReadingTimestamp);
        Assert.Equal(2.8, estimate.UsedFlowCurrentMph);
        Assert.Equal("USGSGauge", estimate.UsedFlowReadingSource);

        // Assert: Current value from flow data
        Assert.Equal(2.8, estimate.RiverCurrentMphUsed);
        Assert.Equal(2.8, estimate.CurrentMphUsed);
        Assert.Equal("flow reading", estimate.CurrentSource);
        Assert.Contains("flow reading", estimate.ExplanationSummary);
    }

    /// <summary>
    /// Tests that traceability fields are null when fallback is used (no flow data).
    /// </summary>
    [Fact]
    public void FlowVisibility_Estimate_TraceabilityNull_WhenFallbackUsed()
    {
        // Arrange: No flow data
        var flowService = new InMemoryFlowReadingService();
        var estimationService = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = Guid.NewGuid(),
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 1.5,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var estimate = estimationService.Estimate(request);

        // Assert: Traceability fields null (fallback used)
        Assert.Null(estimate.UsedFlowReadingId);
        Assert.Null(estimate.UsedFlowReadingTimestamp);
        Assert.Null(estimate.UsedFlowCurrentMph);
        Assert.Null(estimate.UsedFlowReadingSource);

        // Assert: Manual current used
        Assert.Equal(1.5, estimate.RiverCurrentMphUsed);
        Assert.Contains("manually entered", estimate.Assumptions, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("manual current", estimate.CurrentSource);
        Assert.Contains("manual current", estimate.ExplanationSummary);
    }

    /// <summary>
    /// Tests that when no FlowReadingService is provided, traceability fields remain null.
    /// </summary>
    [Fact]
    public void FlowVisibility_Estimate_TraceabilityNull_WhenNoFlowService()
    {
        // Arrange: No flow service
        var estimationService = new TripEstimationService(); // null flow service

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
        var estimate = estimationService.Estimate(request);

        // Assert: All traceability fields null
        Assert.Null(estimate.UsedFlowReadingId);
        Assert.Null(estimate.UsedFlowReadingTimestamp);
        Assert.Null(estimate.UsedFlowCurrentMph);
        Assert.Null(estimate.UsedFlowReadingSource);
        Assert.Equal("manual current", estimate.CurrentSource);
    }

    /// <summary>
    /// Tests that when multiple flow readings exist, the latest is used and tracked.
    /// </summary>
    [Fact]
    public void FlowVisibility_Estimate_UsesLatestFlow_AndTracksIt()
    {
        // Arrange
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();

        var oldReading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow.AddHours(-2),
            EstimatedCurrentMph = 1.5,
            Source = "OldGauge"
        };

        var newerReading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            EstimatedCurrentMph = 2.9,
            Source = "NewGauge"
        };

        flowService.AddFlowReading(oldReading);
        flowService.AddFlowReading(newerReading);

        var estimationService = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 0,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var estimate = estimationService.Estimate(request);

        // Assert: Newer reading used and tracked
        Assert.Equal(newerReading.Id, estimate.UsedFlowReadingId);
        Assert.Equal(newerReading.ObservedAtUtc, estimate.UsedFlowReadingTimestamp);
        Assert.Equal(2.9, estimate.UsedFlowCurrentMph);
        Assert.Equal("NewGauge", estimate.UsedFlowReadingSource);
        Assert.Equal("flow reading", estimate.CurrentSource);
        Assert.Contains("current 2.9 mph", estimate.ExplanationSummary);
    }

    /// <summary>
    /// Tests that flow reading with null EstimatedCurrentMph is not used;
    /// traceability fields remain null and fallback is used.
    /// </summary>
    [Fact]
    public void FlowVisibility_Estimate_SkipsInvalidFlowReading()
    {
        // Arrange
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();

        var invalidReading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow,
            EstimatedCurrentMph = null, // Invalid: no current
            Source = "BadSensor"
        };

        flowService.AddFlowReading(invalidReading);

        var estimationService = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Test Segment",
            DistanceMiles = 10,
            PaddlingSpeedMph = 5,
            RiverCurrentMphOverride = 1.2,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var estimate = estimationService.Estimate(request);

        // Assert: Traceability null (fallback used)
        Assert.Null(estimate.UsedFlowReadingId);
        Assert.Null(estimate.UsedFlowReadingTimestamp);
        Assert.Null(estimate.UsedFlowCurrentMph);
        Assert.Null(estimate.UsedFlowReadingSource);

        // Assert: Manual current used
        Assert.Equal(1.2, estimate.RiverCurrentMphUsed);
        Assert.Equal(1.2, estimate.CurrentMphUsed);
        Assert.Equal("manual current", estimate.CurrentSource);
    }

    /// <summary>
    /// Tests that zero flow current is captured in traceability.
    /// </summary>
    [Fact]
    public void FlowVisibility_Estimate_IncludesZeroFlow_InTraceability()
    {
        // Arrange
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();

        var zeroFlowReading = new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow,
            EstimatedCurrentMph = 0, // Zero is valid
            Source = "StillWaterGauge"
        };

        flowService.AddFlowReading(zeroFlowReading);

        var estimationService = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Still Water",
            DistanceMiles = 5,
            PaddlingSpeedMph = 4,
            RiverCurrentMphOverride = 1.0, // Would be manual fallback if flow wasn't valid
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var estimate = estimationService.Estimate(request);

        // Assert: Zero flow captured in traceability
        Assert.Equal(zeroFlowReading.Id, estimate.UsedFlowReadingId);
        Assert.Equal(0, estimate.UsedFlowCurrentMph);
        Assert.Equal("StillWaterGauge", estimate.UsedFlowReadingSource);
        Assert.Equal(0, estimate.RiverCurrentMphUsed);
        Assert.Equal(0, estimate.CurrentMphUsed);
        Assert.Equal("flow reading", estimate.CurrentSource);
        Assert.Contains("flow reading", estimate.ExplanationSummary);
    }

    /// <summary>
    /// Tests end-to-end ingestion and usage: add flow, estimate uses it, estimate tracks it.
    /// </summary>
    [Fact]
    public void FlowVisibility_EndToEnd_Ingestion_Usage_Tracking()
    {
        // Arrange: Create segment and flow
        var segmentId = new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA");
        var flowService = new InMemoryFlowReadingService();

        // Ingest flow reading
        var flowReading = new FlowReading
        {
            Id = new Guid("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"),
            SegmentId = segmentId,
            ObservedAtUtc = new DateTime(2026, 4, 22, 10, 0, 0),
            EstimatedCurrentMph = 3.2,
            GaugeHeightFeet = 4.5,
            FlowRateCfs = 3500,
            Source = "USGS",
            SourceReference = "Gauge_01234"
        };

        flowService.AddFlowReading(flowReading);

        // Verify ingestion
        var retrieved = flowService.GetLatestForSegment(segmentId);
        Assert.NotNull(retrieved);
        Assert.Equal(3.2, retrieved.EstimatedCurrentMph);

        // Create estimation service and produce estimate
        var estimationService = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Neuse River",
            DistanceMiles = 7.0,
            PaddlingSpeedMph = 5.0,
            RiverCurrentMphOverride = 1.0, // Default would be this
            LaunchTimeLocal = new DateTime(2026, 4, 22, 9, 0, 0)
        };

        // Act
        var estimate = estimationService.Estimate(request);

        // Assert: Flow was used
        Assert.Equal(3.2, estimate.RiverCurrentMphUsed); // From flow, not 1.0
        Assert.Equal(3.2, estimate.CurrentMphUsed);
        Assert.Equal(8.2, estimate.EffectiveSpeedMph); // 5.0 + 3.2
        Assert.Equal("flow reading", estimate.CurrentSource);
        Assert.Contains("Base paddling speed 5 mph + current 3.2 mph", estimate.ExplanationSummary);

        // Assert: Traceability complete
        Assert.Equal(new Guid("BBBBBBBB-BBBB-BBBB-BBBB-BBBBBBBBBBBB"), estimate.UsedFlowReadingId);
        Assert.Equal(new DateTime(2026, 4, 22, 10, 0, 0), estimate.UsedFlowReadingTimestamp);
        Assert.Equal(3.2, estimate.UsedFlowCurrentMph);
        Assert.Equal("USGS", estimate.UsedFlowReadingSource);

        // Assert: Duration calculated correctly (allowing small rounding differences)
        var expectedDurationHours = 7.0 / 8.2;
        Assert.Equal(expectedDurationHours, estimate.EstimatedDuration.TotalHours, precision: 3);
    }
}
