using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace SignalExtraction.Core.Tests;

public class GaugeRegistryApiTests
{
    [Fact]
    public async Task GetGauges_ReturnsSeededPriorityGaugeRegistry()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var gauges = await client.GetFromJsonAsync<List<UsgsGaugeApiResponse>>("/api/gauges");

        Assert.NotNull(gauges);
        Assert.Equal(19, gauges.Count);
        Assert.All(gauges, gauge =>
        {
            Assert.NotEqual(Guid.Empty, gauge.Id);
            Assert.NotEqual(Guid.Empty, gauge.RiverId);
            Assert.False(string.IsNullOrWhiteSpace(gauge.StationId));
            Assert.False(string.IsNullOrWhiteSpace(gauge.Name));
            Assert.Equal("USGS", gauge.Source);
            Assert.Equal(gauge.StationId, gauge.SourceReference);
        });
    }

    [Fact]
    public async Task GetGauges_IncludesProvidedPriorityStations()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var gauges = await client.GetFromJsonAsync<List<UsgsGaugeApiResponse>>("/api/gauges");

        Assert.NotNull(gauges);
        Assert.Contains(gauges, gauge => gauge.StationId == "02087183" && gauge.Name == "Neuse River | Falls Dam (Raleigh)");
        Assert.Contains(gauges, gauge => gauge.StationId == "02087500" && gauge.Name == "Neuse River | Clayton");
        Assert.Contains(gauges, gauge => gauge.StationId == "02096500" && gauge.Name == "Haw River | near Bynum");
        Assert.Contains(gauges, gauge => gauge.StationId == "02099000" && gauge.Name == "Deep River | at Ramseur");
        Assert.Contains(gauges, gauge => gauge.StationId == "02097314" && gauge.Name == "New Hope Creek | near Chapel Hill");
    }

    private sealed class UsgsGaugeApiResponse
    {
        public Guid Id { get; set; }
        public Guid RiverId { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string SourceReference { get; set; } = string.Empty;
        public string? Notes { get; set; }
    }
}
