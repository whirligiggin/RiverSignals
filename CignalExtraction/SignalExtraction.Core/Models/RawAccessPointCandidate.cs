namespace SignalExtraction.Core.Models;

public class RawAccessPointCandidate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string RiverName { get; set; } = string.Empty;
    public string? DescriptiveClues { get; set; }
    public RawAccessPointSourceType SourceType { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; }
    public bool IsResolved { get; set; }
    public RawAccessPointReviewState ReviewState { get; set; } = RawAccessPointReviewState.Unreviewed;
    public string? ReviewerNote { get; set; }
}

public enum RawAccessPointSourceType
{
    State,
    County,
    Municipal,
    Organization
}

public enum RawAccessPointReviewState
{
    Unreviewed,
    NeedsMoreSourceContext,
    DuplicateCandidate,
    LikelyExistingAccessPoint,
    PromotableLater,
    OutOfScope
}
