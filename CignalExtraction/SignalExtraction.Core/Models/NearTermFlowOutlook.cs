namespace SignalExtraction.Core.Models;

public class NearTermFlowOutlook
{
    public Guid SegmentId { get; set; }
    public DateTime LaunchTimeLocal { get; set; }
    public double EstimatedCurrentMph { get; set; }
    public string Source { get; set; } = string.Empty;
    public string Assumptions { get; set; } = string.Empty;
    public Guid BasedOnFlowReadingId { get; set; }
    public DateTime BasedOnObservedAtUtc { get; set; }
    public double BasedOnCurrentMph { get; set; }
    public string? BasedOnFlowReadingSource { get; set; }
}
