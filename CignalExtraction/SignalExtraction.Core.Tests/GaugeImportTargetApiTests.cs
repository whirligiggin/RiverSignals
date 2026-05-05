using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Tests;

public class GaugeImportTargetApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task GetImportTargets_ReturnsReviewableMeaning_ForCurrentMappings()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var targets = await client.GetFromJsonAsync<List<GaugeImportTargetApiResponse>>("/api/gauges/import-targets", JsonOptions);

        Assert.NotNull(targets);
        Assert.Equal(14, targets.Count);
        Assert.All(targets, target =>
        {
            Assert.NotEqual(Guid.Empty, target.GaugeId);
            Assert.NotEqual(Guid.Empty, target.SegmentId);
            Assert.False(string.IsNullOrWhiteSpace(target.StationId));
            Assert.False(string.IsNullOrWhiteSpace(target.GaugeName));
            Assert.False(string.IsNullOrWhiteSpace(target.SegmentName));
            Assert.False(string.IsNullOrWhiteSpace(target.Notes));
        });
        Assert.Contains(targets, target => target.RelationshipType == UsgsGaugeRelationshipType.CorridorReference);
        Assert.Contains(targets, target => target.RelationshipType == UsgsGaugeRelationshipType.LocalReachReference);
        Assert.All(targets, target => Assert.Equal(UsgsGaugeLinkageReviewStatus.Provisional, target.ReviewStatus));
    }

    private sealed class GaugeImportTargetApiResponse
    {
        public Guid GaugeId { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string GaugeName { get; set; } = string.Empty;
        public Guid SegmentId { get; set; }
        public string SegmentName { get; set; } = string.Empty;
        public UsgsGaugeRelationshipType RelationshipType { get; set; }
        public UsgsGaugeLinkageReviewStatus ReviewStatus { get; set; }
        public string? Notes { get; set; }
    }
}
