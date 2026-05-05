using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class AccessPointIdentifierService : IAccessPointIdentifierService
{
    private static readonly Guid RichardsonBridgeCandidateId = new("a1000000-0000-0000-0000-000000000002");
    private static readonly Guid CliffsStateParkCandidateId = new("a1000000-0000-0000-0000-000000000006");
    private static readonly Guid CliffsCountyCandidateId = new("a1000000-0000-0000-0000-000000000015");
    private static readonly Guid AndersonPointTwentyCandidateId = new("a1000000-0000-0000-0000-000000000008");
    private static readonly Guid AndersonPointTwentyTwoCandidateId = new("a1000000-0000-0000-0000-000000000010");

    private static readonly IReadOnlyList<AccessPointIdentifier> SeedIdentifiers = new List<AccessPointIdentifier>
    {
        new()
        {
            Id = new Guid("b1000000-0000-0000-0000-000000000001"),
            RawAccessPointCandidateId = RichardsonBridgeCandidateId,
            PossibleAccessKey = "raw-candidate:richardson-bridge-boat-ramp",
            IdentifierType = AccessPointIdentifierType.SourceListedName,
            IdentifierValue = "Richardson Bridge Boat Ramp",
            SourceName = "Johnston County Parks & Open Space",
            SourceReference = "https://www.johnstonnc.com/parks/pcontent.cfm?id=65",
            Note = "Source-listed candidate name.",
            Status = AccessPointIdentifierStatus.SourceAttributed
        },
        new()
        {
            Id = new Guid("b1000000-0000-0000-0000-000000000002"),
            RawAccessPointCandidateId = RichardsonBridgeCandidateId,
            PossibleAccessKey = "raw-candidate:richardson-bridge-boat-ramp",
            IdentifierType = AccessPointIdentifierType.Address,
            IdentifierValue = "1592 Richardson Bridge Rd, Princeton, NC 27569",
            SourceName = "Johnston County Parks & Open Space",
            SourceReference = "https://www.johnstonnc.com/parks/pcontent.cfm?id=65",
            Note = "Source-listed address retained as an identifier.",
            Status = AccessPointIdentifierStatus.SourceAttributed
        },
        new()
        {
            Id = new Guid("b1000000-0000-0000-0000-000000000003"),
            RawAccessPointCandidateId = CliffsStateParkCandidateId,
            PossibleAccessKey = "possible-access:cliffs-of-the-neuse-state-park",
            IdentifierType = AccessPointIdentifierType.SourceListedName,
            IdentifierValue = "Cliffs of the Neuse State Park",
            SourceName = "North Carolina State Parks",
            SourceReference = "https://www.ncparks.gov/state-parks/cliffs-neuse-state-park",
            Note = "State source candidate name.",
            Status = AccessPointIdentifierStatus.SourceAttributed
        },
        new()
        {
            Id = new Guid("b1000000-0000-0000-0000-000000000004"),
            RawAccessPointCandidateId = CliffsStateParkCandidateId,
            PossibleAccessKey = "possible-access:cliffs-of-the-neuse-state-park",
            IdentifierType = AccessPointIdentifierType.CoordinatePair,
            IdentifierValue = "35.2354,-77.8932",
            SourceName = "North Carolina State Parks",
            SourceReference = "https://www.ncparks.gov/state-parks/cliffs-neuse-state-park",
            Note = "Coordinate pair retained as source-attributed location evidence.",
            Status = AccessPointIdentifierStatus.SourceAttributed
        },
        new()
        {
            Id = new Guid("b1000000-0000-0000-0000-000000000005"),
            RawAccessPointCandidateId = CliffsCountyCandidateId,
            PossibleAccessKey = "possible-access:cliffs-of-the-neuse-state-park",
            IdentifierType = AccessPointIdentifierType.SourceUrl,
            IdentifierValue = "https://waynegov.com/685/Parks",
            SourceName = "Wayne County Parks & Recreation",
            SourceReference = "https://waynegov.com/685/Parks",
            Note = "Second source for a similarly named possible access place; not a merge.",
            Status = AccessPointIdentifierStatus.Provisional
        },
        new()
        {
            Id = new Guid("b1000000-0000-0000-0000-000000000006"),
            RawAccessPointCandidateId = AndersonPointTwentyCandidateId,
            PossibleAccessKey = "possible-access:anderson-point-park",
            IdentifierType = AccessPointIdentifierType.Address,
            IdentifierValue = "20 Anderson Point Dr, Raleigh, NC 27610",
            SourceName = "City of Raleigh Parks & Recreation",
            SourceReference = "https://raleighnc.gov/parks-and-recreation/services/all-about-raleighs-greenways/greenway-parking",
            Note = "One source-listed address for Anderson Point Park.",
            Status = AccessPointIdentifierStatus.Provisional
        },
        new()
        {
            Id = new Guid("b1000000-0000-0000-0000-000000000007"),
            RawAccessPointCandidateId = AndersonPointTwentyTwoCandidateId,
            PossibleAccessKey = "possible-access:anderson-point-park",
            IdentifierType = AccessPointIdentifierType.Address,
            IdentifierValue = "22 Anderson Point Dr, Raleigh, NC 27610",
            SourceName = "City of Raleigh Parks & Recreation",
            SourceReference = "https://raleighnc.gov/parks-and-recreation/services/all-about-raleighs-greenways/greenway-parking",
            Note = "Nearby source-listed address retained separately; not a dedupe.",
            Status = AccessPointIdentifierStatus.Provisional
        }
    };

    public IReadOnlyList<AccessPointIdentifier> GetAccessPointIdentifiers()
    {
        return SeedIdentifiers
            .Select(identifier => new AccessPointIdentifier
            {
                Id = identifier.Id,
                RawAccessPointCandidateId = identifier.RawAccessPointCandidateId,
                CatalogAccessPointId = identifier.CatalogAccessPointId,
                PossibleAccessKey = identifier.PossibleAccessKey,
                IdentifierType = identifier.IdentifierType,
                IdentifierValue = identifier.IdentifierValue,
                SourceName = identifier.SourceName,
                SourceReference = identifier.SourceReference,
                Note = identifier.Note,
                Status = identifier.Status
            })
            .ToList();
    }
}
