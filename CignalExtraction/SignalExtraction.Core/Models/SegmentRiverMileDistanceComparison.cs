namespace SignalExtraction.Core.Models;

public class SegmentRiverMileDistanceComparison
{
    public Guid SegmentId { get; set; }
    public double ExistingDistance { get; set; }
    public double? RiverMileDistance { get; set; }
    public double? Delta { get; set; }
    public DistanceReconciliationReviewClassification ReviewClassification { get; set; }
}

public enum DistanceReconciliationReviewClassification
{
    Aligned,
    SeedIssue,
    AccessPlacementIssue,
    RiverMileAlignmentIssue,
    RequiresReview
}
