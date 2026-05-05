namespace ProjectCignal.Models;

public class TripEstimateRequest
{
    public string SegmentName { get; set; } = string.Empty;
    public double DistanceMiles { get; set; }
    public double PaddlingSpeedMph { get; set; }
    public double RiverCurrentMph { get; set; }
    public DateTime? LaunchTime { get; set; }
}

public class TripEstimateResponse
{
    public string SegmentName { get; set; } = string.Empty;
    public double EstimatedDurationHours { get; set; }
    public string EstimatedDurationText { get; set; } = string.Empty;
    public DateTime? EstimatedFinishTime { get; set; }
    public string Assumptions { get; set; } = string.Empty;
}
