namespace SignalExtraction.Core.Models;

public class AccessPoint
{
    public Guid Id { get; set; }
    public Guid RiverId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? AccessType { get; set; }
    public int RiverOrder { get; set; }
    public bool IsPublic { get; set; } = true;
    public double? RiverMile { get; set; }
    public string? RiverMileSource { get; set; }
    public double? RiverMileConfidence { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public string? ReviewTrigger { get; set; }
    public string? Address { get; set; }
    public string? Amenities { get; set; }
    public string? SourceName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Notes { get; set; }
}
