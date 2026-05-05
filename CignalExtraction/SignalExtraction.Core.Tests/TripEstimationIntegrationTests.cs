using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

/// <summary>
/// Integration tests that verify the complete estimation flow,
/// simulating real usage from ProjectCignal UI.
/// </summary>
public class TripEstimationIntegrationTests
{
    /// <summary>
    /// Simulates: User selects a preset segment with available flow data,
    /// enters paddling speed and launch time, system produces estimate using flow.
    /// </summary>
    [Fact]
    public void Integration_UIFlow_WithFlowDataAvailable()
    {
        // Arrange: Setup segment catalog and flow data
        var segmentId = new Guid("11111111-1111-1111-1111-111111111111");
        var flowService = new InMemoryFlowReadingService();
        
        // Simulate flow reading from sensor/gauge
        flowService.AddFlowReading(new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            EstimatedCurrentMph = 2.0,
            FlowRateCfs = 2500,
            Source = "Gauge",
            SourceReference = "USGS_12345"
        });

        var estimationService = new TripEstimationService(flowService);

        // Simulate UI: User picks "Neuse - Anderson Point to Poole Road"
        var selectedSegment = new Segment
        {
            Id = segmentId,
            Name = "Neuse - Anderson Point to Poole Road",
            DistanceMiles = 7.0,
            DefaultCurrentMph = 1.5 // UI shows this as default hint
        };

        // User input from form
        var userInput = new TripEstimateRequest
        {
            SegmentId = selectedSegment.Id, // UI now passes this
            SegmentName = selectedSegment.Name,
            DistanceMiles = selectedSegment.DistanceMiles,
            PaddlingSpeedMph = 5.0, // User enters
            RiverCurrentMphOverride = selectedSegment.DefaultCurrentMph ?? 0, // Default filled
            LaunchTimeLocal = DateTime.Now
        };

        // Act: Estimate
        var estimate = estimationService.Estimate(userInput);

        // Assert: Should use flow data, not manual default
        Assert.Equal(2.0, estimate.RiverCurrentMphUsed); // From flow, not 1.5
        Assert.Equal(7.0, estimate.EffectiveSpeedMph); // 5.0 + 2.0
        Assert.Contains("flow reading", estimate.Assumptions, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Simulates: User selects segment, but no flow data available,
    /// system falls back to manual/default current.
    /// </summary>
    [Fact]
    public void Integration_UIFlow_WithoutFlowData_FallsBackToManual()
    {
        // Arrange: No flow data in service
        var segmentId = new Guid("22222222-2222-2222-2222-222222222222");
        var flowService = new InMemoryFlowReadingService();
        // Deliberately don't add any flow readings

        var estimationService = new TripEstimationService(flowService);

        // Simulate UI: User picks "Cape Fear - Lillington to Erwin"
        var selectedSegment = new Segment
        {
            Id = segmentId,
            Name = "Cape Fear - Lillington to Erwin",
            DistanceMiles = 10.0,
            DefaultCurrentMph = 2.0
        };

        // User input
        var userInput = new TripEstimateRequest
        {
            SegmentId = selectedSegment.Id,
            SegmentName = selectedSegment.Name,
            DistanceMiles = selectedSegment.DistanceMiles,
            PaddlingSpeedMph = 5.0,
            RiverCurrentMphOverride = selectedSegment.DefaultCurrentMph,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var estimate = estimationService.Estimate(userInput);

        // Assert: Should use manual input
        Assert.Equal(2.0, estimate.RiverCurrentMphUsed);
        Assert.Contains("manually entered", estimate.Assumptions, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Simulates: User manually enters trip without segment selection (ad hoc estimation).
    /// System should work without flow service (backward compatibility).
    /// </summary>
    [Fact]
    public void Integration_AdHocEstimate_WithoutSegmentId()
    {
        // Arrange: No flow service or segment ID
        var estimationService = new TripEstimationService(); // No flow service

        // User enters custom trip details
        var userInput = new TripEstimateRequest
        {
            // SegmentId is null - ad hoc entry
            SegmentName = "Custom Route",
            DistanceMiles = 12.5,
            PaddlingSpeedMph = 4.5,
            RiverCurrentMphOverride = 1.2,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var estimate = estimationService.Estimate(userInput);

        // Assert: Should work without flow lookup
        Assert.Equal(1.2, estimate.RiverCurrentMphUsed);
        Assert.Equal(5.7, estimate.EffectiveSpeedMph, precision: 1);
        Assert.Contains("manually entered", estimate.Assumptions, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies validation still rejects invalid speeds after flow lookup.
    /// </summary>
    [Fact]
    public void Integration_ValidationPreserved_EvenWithFlowService()
    {
        // Arrange: Flow service with data, but user provides negative manual current
        var segmentId = Guid.NewGuid();
        var flowService = new InMemoryFlowReadingService();
        flowService.AddFlowReading(new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = DateTime.UtcNow,
            EstimatedCurrentMph = null, // No valid flow data
            Source = "Sensor"
        });

        var estimationService = new TripEstimationService(flowService);

        var userInput = new TripEstimateRequest
        {
            SegmentId = segmentId,
            SegmentName = "Test",
            DistanceMiles = 10,
            PaddlingSpeedMph = 2.0,
            RiverCurrentMphOverride = -5.0, // Invalid
            LaunchTimeLocal = DateTime.Now
        };

        // Act & Assert: Should reject
        var ex = Assert.Throws<ArgumentException>(() => estimationService.Estimate(userInput));
        Assert.Contains("greater than zero", ex.Message);
    }

    /// <summary>
    /// Verifies that when flow service is not available, system falls back gracefully.
    /// This tests the default behavior for existing code that doesn't use flow.
    /// </summary>
    [Fact]
    public void Integration_BackwardCompatibility_NoFlowService()
    {
        // Arrange: Construct service without flow (simulates old usage pattern)
        var estimationService = new TripEstimationService(null); // Explicitly null

        var request = new TripEstimateRequest
        {
            SegmentId = Guid.NewGuid(), // Even if SegmentId provided
            SegmentName = "Any Segment",
            DistanceMiles = 5.0,
            PaddlingSpeedMph = 4.0,
            RiverCurrentMphOverride = 1.0,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var result = estimationService.Estimate(request);

        // Assert: Should use manual override
        Assert.Equal(1.0, result.RiverCurrentMphUsed);
        Assert.Contains("manually entered", result.Assumptions, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that UI can construct request without SegmentId and still work.
    /// (For ad hoc entry or new segments not in catalog)
    /// </summary>
    [Fact]
    public void Integration_RequestWithoutSegmentId_StillWorks()
    {
        // Arrange
        var flowService = new InMemoryFlowReadingService();
        var estimationService = new TripEstimationService(flowService);

        var request = new TripEstimateRequest
        {
            SegmentId = null, // No segment ID
            SegmentName = "New Segment",
            DistanceMiles = 8.0,
            PaddlingSpeedMph = 5.0,
            RiverCurrentMphOverride = 1.0,
            LaunchTimeLocal = DateTime.Now
        };

        // Act
        var result = estimationService.Estimate(request);

        // Assert: Should work
        Assert.Equal(1.0, result.RiverCurrentMphUsed);
        Assert.True(result.EstimatedDuration.TotalHours > 0);
    }
}
