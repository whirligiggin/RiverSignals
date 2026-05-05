using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IRawAccessPointCandidateService
{
    IReadOnlyList<RawAccessPointCandidate> GetRawAccessPointCandidates();
    RawAccessPointCandidate? UpdateReviewState(Guid candidateId, RawAccessPointReviewState reviewState, string? reviewerNote);
}
