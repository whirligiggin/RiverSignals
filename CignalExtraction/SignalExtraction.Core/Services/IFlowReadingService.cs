using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IFlowReadingService
{
    /// <summary>
    /// Retrieves the most recent FlowReading for a given segment.
    /// </summary>
    /// <param name="segmentId">The segment identifier.</param>
    /// <returns>The latest FlowReading if available; otherwise null.</returns>
    FlowReading? GetLatestForSegment(Guid segmentId);

    /// <summary>
    /// Adds a FlowReading to the system for a segment.
    /// This is the ingestion path for flow data.
    /// </summary>
    /// <param name="reading">The flow reading to ingest.</param>
    void AddFlowReading(FlowReading reading);
}
