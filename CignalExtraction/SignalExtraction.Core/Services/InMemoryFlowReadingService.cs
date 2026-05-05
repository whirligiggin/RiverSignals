using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

/// <summary>
/// In-memory implementation of flow reading lookup and ingestion.
/// Suitable for testing and initial integration.
/// Can be extended or replaced with database-backed implementation.
/// </summary>
public class InMemoryFlowReadingService : IFlowReadingService
{
    private readonly List<FlowReading> _readings = new();

    /// <summary>
    /// Adds (ingests) a flow reading to the in-memory store.
    /// </summary>
    public void AddFlowReading(FlowReading reading)
    {
        _readings.Add(reading);
    }

    /// <summary>
    /// Gets the most recent FlowReading for the given segment.
    /// </summary>
    public FlowReading? GetLatestForSegment(Guid segmentId)
    {
        return _readings
            .Where(r => r.SegmentId == segmentId)
            .OrderByDescending(r => r.ObservedAtUtc)
            .FirstOrDefault();
    }

    /// <summary>
    /// Clears all stored readings (useful for testing).
    /// </summary>
    public void Clear()
    {
        _readings.Clear();
    }
}
