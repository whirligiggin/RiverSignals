namespace SignalExtraction.Core.Models;

public class FlowReading
{
    public Guid Id { get; set; }
    public Guid SegmentId { get; set; }

    public DateTime ObservedAtUtc { get; set; }

    // Keep this flexible early. Add richer hydrology later.
    public double? EstimatedCurrentMph { get; set; }
    public double? GaugeHeightFeet { get; set; }
    public double? FlowRateCfs { get; set; }

    public string Source { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
}