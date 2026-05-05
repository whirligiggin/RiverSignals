using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Tests;

public class AccessPointIdentifierApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task GetAccessPointIdentifiers_ReturnsIdentifierRowsWithSourceProvenance()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var identifiers = await client.GetFromJsonAsync<List<AccessPointIdentifierApiResponse>>(
            "/api/access-point-identifiers",
            JsonOptions);

        Assert.NotNull(identifiers);
        Assert.NotEmpty(identifiers);
        Assert.Contains(identifiers, identifier =>
            identifier.PossibleAccessKey == "raw-candidate:richardson-bridge-boat-ramp" &&
            identifier.IdentifierType == AccessPointIdentifierType.Address &&
            identifier.IdentifierValue == "1592 Richardson Bridge Rd, Princeton, NC 27569" &&
            identifier.SourceName == "Johnston County Parks & Open Space" &&
            identifier.Status == AccessPointIdentifierStatus.SourceAttributed);
        Assert.Contains(identifiers, identifier =>
            identifier.PossibleAccessKey == "possible-access:anderson-point-park" &&
            identifier.IdentifierValue == "22 Anderson Point Dr, Raleigh, NC 27610");
    }

    [Fact]
    public async Task GetAccessPointIdentifiers_ReturnsReadOnlyLedgerWithoutPlanningFields()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/access-point-identifiers");

        Assert.Contains("possibleAccessKey", json);
        Assert.Contains("sourceReference", json);
        Assert.DoesNotContain("riverMile", json);
        Assert.DoesNotContain("segmentId", json);
        Assert.DoesNotContain("gaugeId", json);
    }

    private sealed class AccessPointIdentifierApiResponse
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
        public AccessPointIdentifierStatus Status { get; set; }
    }
}
