namespace SignalExtraction.Core.Models;

public class TripObservation
{
    public Guid Id { get; set; }
    public Guid SegmentId { get; set; }
    public ObservationReviewState ReviewState { get; set; } = ObservationReviewState.Unreviewed;
    public ObservationPipelineStage PipelineStage { get; set; } = ObservationPipelineStage.Structured;
    public DateTime StartTimeLocal { get; set; }
    public DateTime? FinishTimeLocal { get; set; }
    public int? DurationMinutes { get; set; }
    public string? PutInText { get; set; }
    public string? TakeOutText { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
