namespace SignalExtraction.Core.Models;

public class WideRunRequest
{
    public string? RiverName { get; set; }
    public string? PutInText { get; set; }
    public string? TakeOutText { get; set; }
    public string? RoughLocation { get; set; }
    public string? Notes { get; set; }
    public string? SourceHints { get; set; }
    public string? PutInAlias { get; set; }
    public string? TakeOutAlias { get; set; }
}

public record StoredWideRunRequest(
    string RunId,
    string RiverName,
    string PutInText,
    string TakeOutText,
    string ReviewStatus,
    string VerificationStatus,
    string Source,
    string? SourceReference,
    string PutInRawAccessPointCandidateId,
    string TakeOutRawAccessPointCandidateId);

public record WideRunRequestSubmissionResult(
    StoredWideRunRequest Request,
    TripEstimate? Estimate,
    BestAvailableEstimateBasis EstimateBasis);

public record BestAvailableEstimateBasis(
    bool CanEstimate,
    string Status,
    string DistanceBasis,
    double? DistanceMiles,
    string? DistanceSource,
    string CurrentBasis,
    string? MatchedRiverName,
    Guid? MatchedRiverId,
    Guid? MatchedSegmentId,
    string? MatchedPutInName,
    string? MatchedTakeOutName,
    bool IsProvisional,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> ReviewFlags);
