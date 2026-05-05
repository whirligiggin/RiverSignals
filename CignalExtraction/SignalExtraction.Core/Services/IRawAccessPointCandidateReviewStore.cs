using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public interface IRawAccessPointCandidateReviewStore
{
    IReadOnlyDictionary<Guid, RawAccessPointCandidateReviewMetadata> GetReviewMetadata();
    void UpsertReviewMetadata(Guid candidateId, RawAccessPointReviewState reviewState, string? reviewerNote);
}

public record RawAccessPointCandidateReviewMetadata(
    Guid CandidateId,
    RawAccessPointReviewState ReviewState,
    string? ReviewerNote);
