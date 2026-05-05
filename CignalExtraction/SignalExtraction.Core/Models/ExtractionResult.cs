namespace SignalExtraction.Core.Models;

public class ExtractionResult
{
    // Locations
    public string? PutInLocation { get; set; }
    public string? PutInSourceText { get; set; }

    public string? PullOutLocation { get; set; }
    public string? PullOutSourceText { get; set; }

    // Watercraft
    public string? WatercraftType { get; set; }
    public string? WatercraftSourceText { get; set; }

    // Duration
    public double? DurationHours { get; set; }
    public string? DurationSourceText { get; set; }

    // Timing
    public string? TripDateOrTiming { get; set; }
    public string? TripDateOrTimingSourceText { get; set; }

    // Notes
    public string? ConditionsOrNotes { get; set; }

    // Classification
    public RecordType RecordType { get; set; }
    public DurationType DurationType { get; set; }
    public SourceType SourceType { get; set; }

    // Context
    public DateTime? CommunicationDateTime { get; set; }

    // Confidence + Review
    public double ExtractionConfidence { get; set; }
    public bool NeedsReview { get; set; }
    public List<string> ReviewReasons { get; set; } = new();
}