using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class RawAccessPointCandidateApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task GetRawAccessPointCandidates_ReturnsUnresolvedCandidates_WithSourceTraceability()
    {
        await using var factory = CreateFactory(new RawAccessPointCandidateService());
        using var client = factory.CreateClient();

        var candidates = await client.GetFromJsonAsync<List<RawAccessPointCandidateApiResponse>>(
            "/api/raw-access-point-candidates",
            JsonOptions);

        Assert.NotNull(candidates);
        Assert.Equal(15, candidates.Count);
        Assert.All(candidates, candidate =>
        {
            Assert.NotEqual(Guid.Empty, candidate.Id);
            Assert.False(string.IsNullOrWhiteSpace(candidate.Name));
            Assert.False(string.IsNullOrWhiteSpace(candidate.RiverName));
            Assert.False(string.IsNullOrWhiteSpace(candidate.SourceName));
            Assert.False(string.IsNullOrWhiteSpace(candidate.SourceUrl));
            Assert.False(candidate.IsResolved);
            Assert.Equal(RawAccessPointReviewState.Unreviewed, candidate.ReviewState);
            Assert.Null(candidate.ReviewerNote);
        });
        Assert.Contains(candidates, candidate =>
            candidate.Name == "Richardson Bridge Boat Ramp" &&
            candidate.SourceType == RawAccessPointSourceType.County);
        Assert.Contains(candidates, candidate =>
            candidate.Name == "Cool Springs Boating Access Area" &&
            candidate.RiverName == "Swift Creek" &&
            candidate.Latitude == 35.19243027);
        Assert.True(candidates.Count(candidate => candidate.Name == "Anderson Point Park") > 1);
    }

    [Fact]
    public async Task UpdateReviewState_StoresReviewLabelWithoutResolvingCandidate()
    {
        await using var factory = CreateFactory(new RawAccessPointCandidateService());
        using var client = factory.CreateClient();
        var candidates = await client.GetFromJsonAsync<List<RawAccessPointCandidateApiResponse>>(
            "/api/raw-access-point-candidates",
            JsonOptions);
        var candidate = Assert.Single(candidates!, candidate => candidate.Name == "Richardson Bridge Boat Ramp");

        var response = await client.PutAsJsonAsync(
            $"/api/raw-access-point-candidates/{candidate.Id}/review",
            new
            {
                reviewState = RawAccessPointReviewState.PromotableLater,
                reviewerNote = "source looks usable later"
            },
            JsonOptions);

        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<RawAccessPointCandidateApiResponse>(JsonOptions);

        Assert.NotNull(updated);
        Assert.Equal(candidate.Id, updated.Id);
        Assert.Equal("Richardson Bridge Boat Ramp", updated.Name);
        Assert.Equal(RawAccessPointReviewState.PromotableLater, updated.ReviewState);
        Assert.Equal("source looks usable later", updated.ReviewerNote);
        Assert.False(updated.IsResolved);
    }

    private static WebApplicationFactory<Program> CreateFactory(IRawAccessPointCandidateService rawAccessPointCandidateService)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    var serviceDescriptor = services.SingleOrDefault(
                        service => service.ServiceType == typeof(IRawAccessPointCandidateService));
                    var storeDescriptor = services.SingleOrDefault(
                        service => service.ServiceType == typeof(IRawAccessPointCandidateReviewStore));

                    if (serviceDescriptor != null)
                        services.Remove(serviceDescriptor);

                    if (storeDescriptor != null)
                        services.Remove(storeDescriptor);

                    services.AddSingleton(rawAccessPointCandidateService);
                });
            });
    }

    private sealed class RawAccessPointCandidateApiResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string RiverName { get; set; } = string.Empty;
        public string? DescriptiveClues { get; set; }
        public RawAccessPointSourceType SourceType { get; set; }
        public string SourceName { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
        public DateTime CapturedAtUtc { get; set; }
        public bool IsResolved { get; set; }
        public RawAccessPointReviewState ReviewState { get; set; }
        public string? ReviewerNote { get; set; }
    }
}
