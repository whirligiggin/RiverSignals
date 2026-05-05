using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Tests;

public class ProvisionalAccessPairApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task GetProvisionalAccessPairs_ReturnsPairsWithProvenanceAndDistanceBasis()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var pairs = await client.GetFromJsonAsync<List<ProvisionalAccessPairApiResponse>>(
            "/api/provisional-access-pairs",
            JsonOptions);

        Assert.NotNull(pairs);
        Assert.NotEmpty(pairs);
        Assert.Contains(pairs, pair =>
            pair.PutIn.PossibleAccessKey == "possible-access:cliffs-of-the-neuse-state-park" &&
            pair.TakeOut.PossibleAccessKey == "raw-candidate:seven-springs-ncwrc-access" &&
            pair.DistanceMiles == 3.0 &&
            pair.DistanceBasis == ProvisionalAccessPairDistanceBasis.UserSupplied &&
            pair.SourceName == "Internal steward planning");
        Assert.Contains(pairs, pair =>
            pair.PutIn.PossibleAccessKey == "possible-access:anderson-point-park" &&
            pair.DistanceBasis == ProvisionalAccessPairDistanceBasis.NotProvided);
    }

    [Fact]
    public async Task GetProvisionalAccessPairs_ReturnsReadOnlyContextWithoutDurablePlanningFields()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/provisional-access-pairs");

        Assert.Contains("putIn", json);
        Assert.Contains("takeOut", json);
        Assert.Contains("possibleAccessKey", json);
        Assert.Contains("distanceBasis", json);
        Assert.DoesNotContain("segmentId", json);
        Assert.DoesNotContain("startAccessPointId", json);
        Assert.DoesNotContain("endAccessPointId", json);
        Assert.DoesNotContain("catalogAccessPointId", json);
        Assert.DoesNotContain("riverMile", json);
        Assert.DoesNotContain("gaugeId", json);
    }

    private sealed class ProvisionalAccessPairApiResponse
    {
        public Guid Id { get; set; }
        public ProvisionalAccessReferenceApiResponse PutIn { get; set; } = new();
        public ProvisionalAccessReferenceApiResponse TakeOut { get; set; } = new();
        public double? DistanceMiles { get; set; }
        public ProvisionalAccessPairDistanceBasis DistanceBasis { get; set; }
        public string? DistanceSourceName { get; set; }
        public string? DistanceSourceReference { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string? SourceReference { get; set; }
        public string? Note { get; set; }
    }

    private sealed class ProvisionalAccessReferenceApiResponse
    {
        public Guid? RawAccessPointCandidateId { get; set; }
        public string? PossibleAccessKey { get; set; }
        public ProvisionalAccessReferenceBasis Basis { get; set; }
        public string? Label { get; set; }
    }
}
