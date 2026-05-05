namespace SignalExtraction.Core.Models;

public class ProvisionalAccessPair
{
    public Guid Id { get; set; }
    public ProvisionalAccessReference PutIn { get; set; } = new();
    public ProvisionalAccessReference TakeOut { get; set; } = new();
    public double? DistanceMiles { get; set; }
    public ProvisionalAccessPairDistanceBasis DistanceBasis { get; set; } = ProvisionalAccessPairDistanceBasis.NotProvided;
    public string? DistanceSourceName { get; set; }
    public string? DistanceSourceReference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public string? Note { get; set; }
}

public class ProvisionalAccessReference
{
    public Guid? RawAccessPointCandidateId { get; set; }
    public string? PossibleAccessKey { get; set; }
    public ProvisionalAccessReferenceBasis Basis { get; set; } = ProvisionalAccessReferenceBasis.RawCandidate;
    public string? Label { get; set; }
}

public enum ProvisionalAccessReferenceBasis
{
    RawCandidate,
    PossibleAccessKey
}

public enum ProvisionalAccessPairDistanceBasis
{
    NotProvided,
    UserSupplied,
    SourceAttributed
}
