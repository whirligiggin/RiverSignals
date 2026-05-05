using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class AccessPointIdentifierServiceTests
{
    [Fact]
    public void GetAccessPointIdentifiers_ReturnsSourceAttributedIdentifierRows()
    {
        var service = new AccessPointIdentifierService();

        var identifiers = service.GetAccessPointIdentifiers();

        Assert.NotEmpty(identifiers);
        Assert.All(identifiers, identifier =>
        {
            Assert.NotEqual(Guid.Empty, identifier.Id);
            Assert.False(string.IsNullOrWhiteSpace(identifier.PossibleAccessKey));
            Assert.False(string.IsNullOrWhiteSpace(identifier.IdentifierValue));
            Assert.False(string.IsNullOrWhiteSpace(identifier.SourceName));
            Assert.True(Enum.IsDefined(identifier.IdentifierType));
            Assert.True(Enum.IsDefined(identifier.Status));
            Assert.True(identifier.RawAccessPointCandidateId.HasValue || identifier.CatalogAccessPointId.HasValue);
        });
    }

    [Fact]
    public void GetAccessPointIdentifiers_AllowsMultipleIdentifiersForOnePossibleAccess()
    {
        var service = new AccessPointIdentifierService();

        var identifiers = service.GetAccessPointIdentifiers();
        var cliffsIdentifiers = identifiers
            .Where(identifier => identifier.PossibleAccessKey == "possible-access:cliffs-of-the-neuse-state-park")
            .ToList();

        Assert.True(cliffsIdentifiers.Count >= 3);
        Assert.Contains(cliffsIdentifiers, identifier => identifier.IdentifierType == AccessPointIdentifierType.SourceListedName);
        Assert.Contains(cliffsIdentifiers, identifier => identifier.IdentifierType == AccessPointIdentifierType.CoordinatePair);
        Assert.Contains(cliffsIdentifiers, identifier => identifier.IdentifierType == AccessPointIdentifierType.SourceUrl);
        Assert.True(cliffsIdentifiers.Select(identifier => identifier.RawAccessPointCandidateId).Distinct().Count() > 1);
    }

    [Fact]
    public void GetAccessPointIdentifiers_PreservesNearbyAddressConflictWithoutDeduping()
    {
        var service = new AccessPointIdentifierService();

        var identifiers = service.GetAccessPointIdentifiers()
            .Where(identifier => identifier.PossibleAccessKey == "possible-access:anderson-point-park")
            .ToList();

        Assert.Contains(identifiers, identifier => identifier.IdentifierValue == "20 Anderson Point Dr, Raleigh, NC 27610");
        Assert.Contains(identifiers, identifier => identifier.IdentifierValue == "22 Anderson Point Dr, Raleigh, NC 27610");
        Assert.Equal(2, identifiers.Select(identifier => identifier.RawAccessPointCandidateId).Distinct().Count());
    }

    [Fact]
    public void AccessPointIdentifier_DoesNotCarryPromotionOrHydrologyFields()
    {
        var properties = typeof(AccessPointIdentifier)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet();

        Assert.DoesNotContain("RiverMile", properties);
        Assert.DoesNotContain("SegmentId", properties);
        Assert.DoesNotContain("StartAccessPointId", properties);
        Assert.DoesNotContain("EndAccessPointId", properties);
        Assert.DoesNotContain("GaugeId", properties);
    }

    [Fact]
    public void AccessPointIdentifiers_DoNotPromoteCandidatesIntoCatalogAccessPointsOrSegments()
    {
        var identifierService = new AccessPointIdentifierService();
        var catalogService = new SegmentCatalogService();

        var rawCandidateIds = identifierService.GetAccessPointIdentifiers()
            .Where(identifier => identifier.RawAccessPointCandidateId.HasValue)
            .Select(identifier => identifier.RawAccessPointCandidateId!.Value)
            .ToHashSet();
        var accessPointIds = catalogService.GetPresetAccessPoints().Select(accessPoint => accessPoint.Id).ToHashSet();
        var segmentIds = catalogService.GetPresetSegments().Select(segment => segment.Id).ToHashSet();

        Assert.Empty(rawCandidateIds.Intersect(accessPointIds));
        Assert.Empty(rawCandidateIds.Intersect(segmentIds));
    }
}
