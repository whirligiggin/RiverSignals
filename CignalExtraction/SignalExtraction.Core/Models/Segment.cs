namespace SignalExtraction.Core.Models;

public class Segment
{
    public Guid Id { get; set; }
    public Guid RiverId { get; set; }
    public Guid StartAccessPointId { get; set; }
    public Guid EndAccessPointId { get; set; }
    public int RiverOrder { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PutInName { get; set; } = string.Empty;
    public string TakeOutName { get; set; } = string.Empty;

    public double DistanceMiles { get; set; }
    public string? DistanceSource { get; set; }

    public double? PutInRiverMile { get; set; }
    public double? TakeOutRiverMile { get; set; }
    public double? RiverMileDistanceMiles { get; set; }
    public string? PutInAddress { get; set; }
    public string? TakeOutAddress { get; set; }
    public string? PutInAmenities { get; set; }
    public string? TakeOutAmenities { get; set; }
    public string? PlanningSource { get; set; }

    public double? PutInLatitude { get; set; }
    public double? PutInLongitude { get; set; }
    public double? TakeOutLatitude { get; set; }
    public double? TakeOutLongitude { get; set; }

    public double? DefaultCurrentMph { get; set; }
    public bool IsActive { get; set; } = true;
}
