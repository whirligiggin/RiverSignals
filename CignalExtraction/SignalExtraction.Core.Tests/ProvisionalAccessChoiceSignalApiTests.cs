using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Tests;

public class ProvisionalAccessChoiceSignalApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task GetProvisionalAccessChoiceSignals_ReturnsSelectionSignalsWithSourceProvenance()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var signals = await client.GetFromJsonAsync<List<ProvisionalAccessChoiceSignalApiResponse>>(
            "/api/provisional-access-choice-signals",
            JsonOptions);

        Assert.NotNull(signals);
        Assert.NotEmpty(signals);
        Assert.Contains(signals, signal =>
            signal.RawAccessPointCandidateId == new Guid("a1000000-0000-0000-0000-000000000002") &&
            signal.PossibleAccessKey == "raw-candidate:richardson-bridge-boat-ramp" &&
            signal.SignalSource == ProvisionalAccessChoiceSignalSource.StewardSelected &&
            signal.SourceName == "Internal steward review");
        Assert.Contains(signals, signal =>
            signal.PossibleAccessKey == "possible-access:anderson-point-park" &&
            signal.SignalSource == ProvisionalAccessChoiceSignalSource.UserSelected);
    }

    [Fact]
    public async Task GetProvisionalAccessChoiceSignals_ReturnsReadOnlySignalWithoutPlanningFields()
    {
        await using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync("/api/provisional-access-choice-signals");

        Assert.Contains("possibleAccessKey", json);
        Assert.Contains("signalSource", json);
        Assert.Contains("selectedAtUtc", json);
        Assert.DoesNotContain("reviewState", json);
        Assert.DoesNotContain("identifierType", json);
        Assert.DoesNotContain("catalogAccessPointId", json);
        Assert.DoesNotContain("riverMile", json);
        Assert.DoesNotContain("segmentId", json);
        Assert.DoesNotContain("gaugeId", json);
    }

    private sealed class ProvisionalAccessChoiceSignalApiResponse
    {
        public Guid Id { get; set; }
        public Guid? RawAccessPointCandidateId { get; set; }
        public string? PossibleAccessKey { get; set; }
        public ProvisionalAccessChoiceSignalSource SignalSource { get; set; }
        public DateTime SelectedAtUtc { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string? SourceReference { get; set; }
        public string? Note { get; set; }
    }
}
