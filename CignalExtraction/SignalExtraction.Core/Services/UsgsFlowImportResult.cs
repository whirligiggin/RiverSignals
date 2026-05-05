namespace SignalExtraction.Core.Services;

public class UsgsFlowImportResult
{
    public int GaugesQueried { get; set; }
    public int GaugeReadingsReceived { get; set; }
    public int FlowReadingsStored { get; set; }
    public List<ImportedSegmentFlowReading> ImportedReadings { get; set; } = [];
}

public class ImportedSegmentFlowReading
{
    public Guid SegmentId { get; set; }
    public Guid GaugeId { get; set; }
    public string StationId { get; set; } = string.Empty;
    public string GaugeName { get; set; } = string.Empty;
    public DateTime ObservedAtUtc { get; set; }
    public double? GaugeHeightFeet { get; set; }
    public double? FlowRateCfs { get; set; }
}
