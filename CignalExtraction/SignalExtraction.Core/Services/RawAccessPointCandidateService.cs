using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class RawAccessPointCandidateService : IRawAccessPointCandidateService
{
    private static readonly DateTime SeedCapturedAtUtc = new(2026, 4, 27, 0, 0, 0, DateTimeKind.Utc);

    private static readonly IReadOnlyList<RawAccessPointCandidate> SeedRawCandidates = new List<RawAccessPointCandidate>
    {
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000001"),
            Name = "Smithfield Town Commons Park",
            Address = "200 S Front St, Smithfield, NC 27577",
            RiverName = "Neuse River",
            DescriptiveClues = "Municipal park along the Neuse River; greenway access; boat ramp.",
            SourceType = RawAccessPointSourceType.County,
            SourceName = "Johnston County Parks & Open Space",
            SourceUrl = "https://www.johnstonnc.com/parks/pcontent.cfm?id=26",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000002"),
            Name = "Richardson Bridge Boat Ramp",
            Address = "1592 Richardson Bridge Rd, Princeton, NC 27569",
            RiverName = "Neuse River",
            DescriptiveClues = "Rural boat ramp that provides access to Neuse River.",
            SourceType = RawAccessPointSourceType.County,
            SourceName = "Johnston County Parks & Open Space",
            SourceUrl = "https://www.johnstonnc.com/parks/pcontent.cfm?id=65",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000003"),
            Name = "NC Highway 111 South Broadhurst Bridge Access",
            Latitude = 35.2609,
            Longitude = -77.9070,
            RiverName = "Neuse River",
            DescriptiveClues = "Paddling launch at the intersection of River Road and Mince Hill Road below Broadhurst Bridge.",
            SourceType = RawAccessPointSourceType.State,
            SourceName = "North Carolina State Parks",
            SourceUrl = "https://www.ncparks.gov/state-parks/cliffs-neuse-state-park/activities",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000004"),
            Name = "Cliffs of the Neuse State Park Sandbar",
            RiverName = "Neuse River",
            DescriptiveClues = "Paddling launch inside the park at the sandbar on the Spanish Moss Trail.",
            SourceType = RawAccessPointSourceType.State,
            SourceName = "North Carolina State Parks",
            SourceUrl = "https://www.ncparks.gov/state-parks/cliffs-neuse-state-park/activities",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000005"),
            Name = "Seven Springs NCWRC Access",
            Address = "100-B Main Street, Seven Springs, NC",
            Latitude = 35.2287,
            Longitude = -77.8460,
            RiverName = "Neuse River",
            DescriptiveClues = "Final launch or take-out downriver from Cliffs of the Neuse; managed by NCWRC.",
            SourceType = RawAccessPointSourceType.State,
            SourceName = "North Carolina State Parks",
            SourceUrl = "https://www.ncparks.gov/state-parks/cliffs-neuse-state-park/activities",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000006"),
            Name = "Cliffs of the Neuse State Park",
            Address = "240 Park Entrance Road, Seven Springs, NC 28578",
            Latitude = 35.2354,
            Longitude = -77.8932,
            RiverName = "Neuse River",
            DescriptiveClues = "State park on the Neuse River with paddling and water recreation context.",
            SourceType = RawAccessPointSourceType.State,
            SourceName = "North Carolina State Parks",
            SourceUrl = "https://www.ncparks.gov/state-parks/cliffs-neuse-state-park",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000007"),
            Name = "Falls Lake Dam Recreation Area",
            Address = "12098 Old Falls of Neuse Road, Wake Forest, NC 27587",
            RiverName = "Neuse River",
            DescriptiveClues = "Greenway parking listing notes canoe access.",
            SourceType = RawAccessPointSourceType.Municipal,
            SourceName = "City of Raleigh Parks & Recreation",
            SourceUrl = "https://raleighnc.gov/parks-and-recreation/services/all-about-raleighs-greenways/greenway-parking",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000008"),
            Name = "Anderson Point Park",
            Address = "20 Anderson Point Dr, Raleigh, NC 27610",
            RiverName = "Neuse River",
            DescriptiveClues = "Greenway parking listing; Anderson Point Park near Neuse River Trail and Crabtree Creek Trail.",
            SourceType = RawAccessPointSourceType.Municipal,
            SourceName = "City of Raleigh Parks & Recreation",
            SourceUrl = "https://raleighnc.gov/parks-and-recreation/services/all-about-raleighs-greenways/greenway-parking",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000009"),
            Name = "Poole Road Canoe Access",
            Address = "6405 Poole Rd, Raleigh, NC 27610",
            RiverName = "Neuse River",
            DescriptiveClues = "Greenway parking listing notes canoe access.",
            SourceType = RawAccessPointSourceType.Municipal,
            SourceName = "City of Raleigh Parks & Recreation",
            SourceUrl = "https://raleighnc.gov/parks-and-recreation/services/all-about-raleighs-greenways/greenway-parking",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000010"),
            Name = "Anderson Point Park",
            Address = "22 Anderson Point Dr, Raleigh, NC 27610",
            RiverName = "Neuse River",
            DescriptiveClues = "Greenway parking listing notes canoe access.",
            SourceType = RawAccessPointSourceType.Municipal,
            SourceName = "City of Raleigh Parks & Recreation",
            SourceUrl = "https://raleighnc.gov/parks-and-recreation/services/all-about-raleighs-greenways/greenway-parking",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000011"),
            Name = "Lawson Creek Park",
            RiverName = "Trent River",
            DescriptiveClues = "Waterfront park context near Lawson Creek and Trent River; source context indicates boat launches.",
            SourceType = RawAccessPointSourceType.Municipal,
            SourceName = "City of New Bern Parks & Recreation",
            SourceUrl = "https://www.newbernnc.gov/government/departments/parks_and_recreation/index.php",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000012"),
            Name = "Union Point Park",
            RiverName = "Neuse River",
            DescriptiveClues = "Park near the Neuse and Trent River confluence.",
            SourceType = RawAccessPointSourceType.Municipal,
            SourceName = "City of New Bern Parks & Recreation",
            SourceUrl = "https://www.newbernnc.gov/government/departments/parks_and_recreation/index.php",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000013"),
            Name = "Cow Pen Landing Boating Access Area",
            Address = "1199 Cow Pen Landing Rd, Vanceboro, NC 28586",
            Latitude = 35.2386391,
            Longitude = -77.16682253,
            RiverName = "Neuse River",
            DescriptiveClues = "NCWRC boating access area; boat ramp; canoe.",
            SourceType = RawAccessPointSourceType.State,
            SourceName = "North Carolina Wildlife Resources Commission",
            SourceUrl = "https://www.ncpaws.org/ncwrcmaps/boatingaccessareas",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000014"),
            Name = "Cool Springs Boating Access Area",
            Address = "1065 Cool Springs Rd, New Bern, NC 28562",
            Latitude = 35.19243027,
            Longitude = -77.08366945,
            RiverName = "Swift Creek",
            DescriptiveClues = "NCWRC boating access area on Swift Creek; boat ramp.",
            SourceType = RawAccessPointSourceType.State,
            SourceName = "North Carolina Wildlife Resources Commission",
            SourceUrl = "https://www.ncpaws.org/ncwrcmaps/boatingaccessareas",
            CapturedAtUtc = SeedCapturedAtUtc
        },
        new()
        {
            Id = new Guid("a1000000-0000-0000-0000-000000000015"),
            Name = "Cliffs of the Neuse State Park",
            Address = "240 Park Entrance Rd, Seven Springs, NC 28578",
            RiverName = "Neuse River",
            DescriptiveClues = "Wayne County parks listing for Cliffs of the Neuse State Park along the southern banks of the Neuse River.",
            SourceType = RawAccessPointSourceType.County,
            SourceName = "Wayne County Parks & Recreation",
            SourceUrl = "https://waynegov.com/685/Parks",
            CapturedAtUtc = SeedCapturedAtUtc
        }
    };

    private readonly IRawAccessPointCandidateReviewStore? _reviewStore;
    private readonly List<RawAccessPointCandidate> _rawCandidates;

    public RawAccessPointCandidateService()
        : this(null)
    {
    }

    public RawAccessPointCandidateService(IRawAccessPointCandidateReviewStore? reviewStore)
    {
        _reviewStore = reviewStore;
        _rawCandidates = SeedRawCandidates.Select(CloneCandidate).ToList();
        ApplyReviewMetadata();
    }

    public IReadOnlyList<RawAccessPointCandidate> GetRawAccessPointCandidates()
    {
        ApplyReviewMetadata();
        return _rawCandidates;
    }

    public RawAccessPointCandidate? UpdateReviewState(Guid candidateId, RawAccessPointReviewState reviewState, string? reviewerNote)
    {
        var candidate = _rawCandidates.SingleOrDefault(candidate => candidate.Id == candidateId);
        if (candidate == null)
            return null;

        candidate.ReviewState = reviewState;
        candidate.ReviewerNote = string.IsNullOrWhiteSpace(reviewerNote)
            ? null
            : reviewerNote.Trim();
        candidate.IsResolved = false;
        _reviewStore?.UpsertReviewMetadata(candidate.Id, candidate.ReviewState, candidate.ReviewerNote);

        return candidate;
    }

    private void ApplyReviewMetadata()
    {
        var reviewMetadata = _reviewStore?.GetReviewMetadata();
        if (reviewMetadata == null || reviewMetadata.Count == 0)
            return;

        foreach (var candidate in _rawCandidates)
        {
            if (!reviewMetadata.TryGetValue(candidate.Id, out var metadata))
                continue;

            candidate.ReviewState = metadata.ReviewState;
            candidate.ReviewerNote = metadata.ReviewerNote;
            candidate.IsResolved = false;
        }
    }

    private static RawAccessPointCandidate CloneCandidate(RawAccessPointCandidate candidate)
    {
        return new RawAccessPointCandidate
        {
            Id = candidate.Id,
            Name = candidate.Name,
            Address = candidate.Address,
            Latitude = candidate.Latitude,
            Longitude = candidate.Longitude,
            RiverName = candidate.RiverName,
            DescriptiveClues = candidate.DescriptiveClues,
            SourceType = candidate.SourceType,
            SourceName = candidate.SourceName,
            SourceUrl = candidate.SourceUrl,
            CapturedAtUtc = candidate.CapturedAtUtc,
            IsResolved = candidate.IsResolved,
            ReviewState = candidate.ReviewState,
            ReviewerNote = candidate.ReviewerNote
        };
    }
}
