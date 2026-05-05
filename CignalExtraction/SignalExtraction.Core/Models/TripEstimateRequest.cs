namespace SignalExtraction.Core.Models;

public class TripEstimateRequest
{
    public Guid? SegmentId { get; set; }

    // Allow ad hoc estimating before segment catalog is complete.
    public string SegmentName { get; set; } = string.Empty;
    public double DistanceMiles { get; set; }

    public double PaddlingSpeedMph { get; set; }
    public double? RiverCurrentMphOverride { get; set; }

    public DateTime? LaunchTimeLocal { get; set; }
}