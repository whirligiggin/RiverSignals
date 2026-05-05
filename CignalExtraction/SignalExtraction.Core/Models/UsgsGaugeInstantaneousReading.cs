namespace SignalExtraction.Core.Models;

public class UsgsGaugeInstantaneousReading
{
    public Guid GaugeId { get; set; }
    public string StationId { get; set; } = string.Empty;
    public string GaugeName { get; set; } = string.Empty;
    public DateTime ObservedAtUtc { get; set; }
    public double? GaugeHeightFeet { get; set; }
    public double? FlowRateCfs { get; set; }
}
