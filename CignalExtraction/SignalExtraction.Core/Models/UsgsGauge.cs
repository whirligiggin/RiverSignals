namespace SignalExtraction.Core.Models;

public class UsgsGauge
{
    public Guid Id { get; set; }
    public Guid RiverId { get; set; }
    public string StationId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = "USGS";
    public string SourceReference { get; set; } = string.Empty;
    public double? RiverMile { get; set; }
    public string? RiverMileSource { get; set; }
    public double? RiverMileConfidence { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public string? ReviewTrigger { get; set; }
    public string? Notes { get; set; }
}
