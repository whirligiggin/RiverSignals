namespace SignalExtraction.Core.Models;

public class ExtractionPlanningHandoffInput
{
    public ExtractionResult Extraction { get; set; } = new();

    public Guid? SegmentId { get; set; }
    public string? SegmentName { get; set; }
    public double? DistanceMiles { get; set; }
    public double? PaddlingSpeedMph { get; set; }
    public double? RiverCurrentMphOverride { get; set; }
    public DateTime? LaunchTimeLocal { get; set; }
}
