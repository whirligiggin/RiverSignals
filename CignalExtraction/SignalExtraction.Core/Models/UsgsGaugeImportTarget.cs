namespace SignalExtraction.Core.Models;

public class UsgsGaugeImportTarget
{
    public Guid GaugeId { get; set; }
    public Guid SegmentId { get; set; }
    public UsgsGaugeRelationshipType RelationshipType { get; set; }
    public UsgsGaugeLinkageReviewStatus ReviewStatus { get; set; }
    public double? MappingConfidence { get; set; }
    public string? MappingConfidenceSource { get; set; }
    public string? Notes { get; set; }
}

public enum UsgsGaugeRelationshipType
{
    LocalReachReference = 0,
    CorridorReference = 1
}

public enum UsgsGaugeLinkageReviewStatus
{
    Provisional = 0,
    OperatorSeeded = 1
}
