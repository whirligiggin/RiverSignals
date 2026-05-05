using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class RawAccessCandidateReviewUiTests
{
    [Fact]
    public async Task InternalRawAccessCandidateReviewPage_ReturnsReviewSurface()
    {
        await using var factory = CreateFactory(new RawAccessPointCandidateService());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/internal/raw-access-candidates");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("<title>Raw Access Candidate Review</title>", html);
        Assert.Contains("Internal review for raw, unresolved source records.", html);
        Assert.Contains("Review labels do not create access points, runs, river miles, gauges, or planning entities.", html);
        Assert.Contains("Smithfield Town Commons Park", html);
        Assert.Contains("North Carolina Wildlife Resources Commission", html);
        Assert.Contains("needs_more_source_context", html);
        Assert.Contains("duplicate_candidate", html);
        Assert.Contains("likely_existing_access_point", html);
        Assert.Contains("promotable_later", html);
        Assert.Contains("out_of_scope", html);
        Assert.Contains("/api/raw-access-point-candidates/${candidateId}/review", html);
        Assert.DoesNotContain("/api/segments/${segmentId}/estimate", html);
    }

    [Fact]
    public async Task InternalRawAccessCandidateReviewPage_ShowsWorklistFiltersAndCounts()
    {
        var service = new RawAccessPointCandidateService();
        var candidate = service.GetRawAccessPointCandidates().First();
        service.UpdateReviewState(
            candidate.Id,
            RawAccessPointReviewState.DuplicateCandidate,
            "reviewed for worklist test");
        await using var factory = CreateFactory(service);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/internal/raw-access-candidates");

        Assert.Contains("Raw candidate worklist status", html);
        Assert.Contains("id=\"reviewStateFilter\"", html);
        Assert.Contains("id=\"sourceFilter\"", html);
        Assert.Contains("id=\"visibleCandidateCount\"", html);
        Assert.Contains("unreviewed: <strong>14</strong>", html);
        Assert.Contains("duplicate_candidate: <strong>1</strong>", html);
        Assert.Contains("data-review-state=\"DuplicateCandidate\"", html);
        Assert.Contains("data-source-name=\"Johnston County Parks &amp; Open Space\"", html);
        Assert.Contains("reviewStateFilter.addEventListener('change', applyFilters);", html);
        Assert.Contains("sourceFilter.addEventListener('change', applyFilters);", html);
    }

    [Fact]
    public async Task InternalRawAccessCandidateReviewPage_DoesNotRedirect()
    {
        await using var factory = CreateFactory(new RawAccessPointCandidateService());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/internal/raw-access-candidates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
}
