namespace SignalExtraction.Core.Models;

public class TripEstimate
{
    public string SegmentName { get; set; } = string.Empty;

    public double DistanceMiles { get; set; }
    public double PaddlingSpeedMph { get; set; }
    public double RiverCurrentMphUsed { get; set; }
    public double CurrentMphUsed { get; set; }
    public double EffectiveSpeedMph { get; set; }

    public TimeSpan EstimatedDuration { get; set; }
    public DateTime? EstimatedFinishTimeLocal { get; set; }

    public string Assumptions { get; set; } = string.Empty;
    public string CurrentSource { get; set; } = string.Empty;
    public string ConditionBasis { get; set; } = string.Empty;
    public string ConditionBasisDetail { get; set; } = string.Empty;
    public string ExplanationSummary { get; set; } = string.Empty;

    // Flow data traceability: populated when flow reading was used for estimation
    public Guid? UsedFlowReadingId { get; set; }
    public DateTime? UsedFlowReadingTimestamp { get; set; }
    public double? UsedFlowCurrentMph { get; set; }
    public string? UsedFlowReadingSource { get; set; }
}
