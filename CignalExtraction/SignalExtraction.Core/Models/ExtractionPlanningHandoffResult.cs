namespace SignalExtraction.Core.Models;

public class ExtractionPlanningHandoffResult
{
    public bool CanEstimate { get; set; }
    public Guid? SegmentId { get; set; }
    public string? SegmentName { get; set; }
    public double? DistanceMiles { get; set; }
    public double? PaddlingSpeedMph { get; set; }
    public double? RiverCurrentMphOverride { get; set; }
    public DateTime? LaunchTimeLocal { get; set; }
    public List<string> AvailableInputs { get; set; } = new();
    public List<string> MissingInputs { get; set; } = new();
    public List<string> AmbiguityFlags { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
}
