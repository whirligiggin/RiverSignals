using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class ProvisionalAccessPairService : IProvisionalAccessPairService
{
    private static readonly DateTime SeedCreatedAtUtc = new(2026, 4, 28, 0, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<ProvisionalAccessPair> SeedPairs = new List<ProvisionalAccessPair>
    {
        new()
        {
            Id = new Guid("d1000000-0000-0000-0000-000000000001"),
            PutIn = new ProvisionalAccessReference
            {
                RawAccessPointCandidateId = new Guid("a1000000-0000-0000-0000-000000000006"),
                PossibleAccessKey = "possible-access:cliffs-of-the-neuse-state-park",
                Basis = ProvisionalAccessReferenceBasis.PossibleAccessKey,
                Label = "Cliffs of the Neuse State Park"
            },
            TakeOut = new ProvisionalAccessReference
            {
                RawAccessPointCandidateId = new Guid("a1000000-0000-0000-0000-000000000005"),
                PossibleAccessKey = "raw-candidate:seven-springs-ncwrc-access",
                Basis = ProvisionalAccessReferenceBasis.RawCandidate,
                Label = "Seven Springs NCWRC Access"
            },
            DistanceMiles = 3.0,
            DistanceBasis = ProvisionalAccessPairDistanceBasis.UserSupplied,
            DistanceSourceName = "Internal planning note",
            CreatedAtUtc = SeedCreatedAtUtc,
            SourceName = "Internal steward planning",
            SourceReference = "provisional-access-pair-planning-v1",
            Note = "Provisional pair only; not a catalog segment or verified run."
        },
        new()
        {
            Id = new Guid("d1000000-0000-0000-0000-000000000002"),
            PutIn = new ProvisionalAccessReference
            {
                RawAccessPointCandidateId = new Guid("a1000000-0000-0000-0000-000000000008"),
                PossibleAccessKey = "possible-access:anderson-point-park",
                Basis = ProvisionalAccessReferenceBasis.PossibleAccessKey,
                Label = "Anderson Point Park"
            },
            TakeOut = new ProvisionalAccessReference
            {
                RawAccessPointCandidateId = new Guid("a1000000-0000-0000-0000-000000000009"),
                PossibleAccessKey = "raw-candidate:poole-road-canoe-access",
                Basis = ProvisionalAccessReferenceBasis.RawCandidate,
                Label = "Poole Road Canoe Access"
            },
            DistanceBasis = ProvisionalAccessPairDistanceBasis.NotProvided,
            CreatedAtUtc = SeedCreatedAtUtc,
            SourceName = "User planning selection",
            SourceReference = "seeded-provisional-pair",
            Note = "Candidate-based planning context retained separately from seeded catalog runs."
        }
    };

    private readonly List<ProvisionalAccessPair> _pairs;

    public ProvisionalAccessPairService()
    {
        _pairs = SeedPairs.Select(ClonePair).ToList();
    }

    public IReadOnlyList<ProvisionalAccessPair> GetProvisionalAccessPairs()
    {
        return _pairs.Select(ClonePair).ToList();
    }

    public ProvisionalAccessPair RecordProvisionalAccessPair(ProvisionalAccessPair pair)
    {
        ValidatePair(pair);

        var stored = ClonePair(pair);
        stored.Id = stored.Id == Guid.Empty ? Guid.NewGuid() : stored.Id;
        stored.PutIn = NormalizeReference(stored.PutIn);
        stored.TakeOut = NormalizeReference(stored.TakeOut);
        stored.SourceName = stored.SourceName.Trim();
        stored.SourceReference = NormalizeOptional(stored.SourceReference);
        stored.DistanceSourceName = NormalizeOptional(stored.DistanceSourceName);
        stored.DistanceSourceReference = NormalizeOptional(stored.DistanceSourceReference);
        stored.Note = NormalizeOptional(stored.Note);
        _pairs.Add(stored);

        return ClonePair(stored);
    }

    private static void ValidatePair(ProvisionalAccessPair pair)
    {
        ValidateReference(pair.PutIn, "put-in");
        ValidateReference(pair.TakeOut, "take-out");

        if (pair.DistanceMiles.HasValue && pair.DistanceMiles <= 0)
            throw new ArgumentException("Provisional distance must be greater than zero when provided.", nameof(pair));

        if (pair.DistanceMiles.HasValue && pair.DistanceBasis == ProvisionalAccessPairDistanceBasis.NotProvided)
            throw new ArgumentException("Provisional distance basis is required when distance is provided.", nameof(pair));

        if (!pair.DistanceMiles.HasValue && pair.DistanceBasis != ProvisionalAccessPairDistanceBasis.NotProvided)
            throw new ArgumentException("Provisional distance basis must be NotProvided when distance is absent.", nameof(pair));

        if (string.IsNullOrWhiteSpace(pair.SourceName))
            throw new ArgumentException("Provisional pair source name is required.", nameof(pair));

        if (pair.CreatedAtUtc == default)
            throw new ArgumentException("Provisional pair creation timestamp is required.", nameof(pair));
    }

    private static void ValidateReference(ProvisionalAccessReference reference, string name)
    {
        if (reference.RawAccessPointCandidateId == null && string.IsNullOrWhiteSpace(reference.PossibleAccessKey))
            throw new ArgumentException($"A provisional {name} reference must include a raw candidate or possible access key.", nameof(reference));
    }

    private static ProvisionalAccessReference NormalizeReference(ProvisionalAccessReference reference)
    {
        return new ProvisionalAccessReference
        {
            RawAccessPointCandidateId = reference.RawAccessPointCandidateId,
            PossibleAccessKey = NormalizeOptional(reference.PossibleAccessKey),
            Basis = reference.Basis,
            Label = NormalizeOptional(reference.Label)
        };
    }

    private static ProvisionalAccessPair ClonePair(ProvisionalAccessPair pair)
    {
        return new ProvisionalAccessPair
        {
            Id = pair.Id,
            PutIn = CloneReference(pair.PutIn),
            TakeOut = CloneReference(pair.TakeOut),
            DistanceMiles = pair.DistanceMiles,
            DistanceBasis = pair.DistanceBasis,
            DistanceSourceName = pair.DistanceSourceName,
            DistanceSourceReference = pair.DistanceSourceReference,
            CreatedAtUtc = pair.CreatedAtUtc,
            SourceName = pair.SourceName,
            SourceReference = pair.SourceReference,
            Note = pair.Note
        };
    }

    private static ProvisionalAccessReference CloneReference(ProvisionalAccessReference reference)
    {
        return new ProvisionalAccessReference
        {
            RawAccessPointCandidateId = reference.RawAccessPointCandidateId,
            PossibleAccessKey = reference.PossibleAccessKey,
            Basis = reference.Basis,
            Label = reference.Label
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
