namespace SignalExtraction.Core.Models;

public class AccessPointIdentifier
{
    public Guid Id { get; set; }
    public Guid? RawAccessPointCandidateId { get; set; }
    public Guid? CatalogAccessPointId { get; set; }
    public string PossibleAccessKey { get; set; } = string.Empty;
    public AccessPointIdentifierType IdentifierType { get; set; }
    public string IdentifierValue { get; set; } = string.Empty;
    public string SourceName { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public string? Note { get; set; }
    public AccessPointIdentifierStatus Status { get; set; } = AccessPointIdentifierStatus.Provisional;
}

public enum AccessPointIdentifierType
{
    SourceListedName,
    Address,
    CoordinatePair,
    SourceUrl,
    AgencyMapReference,
    AlternateName
}

public enum AccessPointIdentifierStatus
{
    Provisional,
    SourceAttributed,
    NeedsReview,
    Conflicting
}
