namespace SignalExtraction.Core.Models;

public enum ObservationPipelineStage
{
    Raw = 0,
    Structured = 1,
    Normalized = 2,
    Reviewed = 3,
    Promoted = 4
}
