using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Tests;

public class WideRunRequestApiTests
{
    [Fact]
    public async Task RequestRunPage_ReturnsPublicIntakeSurface()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/request-run");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>Request a Run</title>", html);
        Assert.Contains("href=\"/\">Segment Estimate</a>", html);
        Assert.Contains("href=\"/compare-runs\">Compare Runs</a>", html);
        Assert.Contains("href=\"/completed-run\">Report Completed Run</a>", html);
        Assert.Contains("class=\"active\" href=\"/request-run\">Request Any Run</a>", html);
        Assert.Contains("Account placeholder", html);
        Assert.Contains("id=\"riverNameInput\"", html);
        Assert.Contains("id=\"putInInput\"", html);
        Assert.Contains("id=\"takeOutInput\"", html);
        Assert.Contains("id=\"sourceHintsInput\"", html);
        Assert.Contains("returns a first estimate immediately", html);
        Assert.Contains("/api/run-requests", html);
        Assert.DoesNotContain("/api/segments/${segmentId}/estimate", html);
        Assert.DoesNotContain("href=\"/#compare\"", html);
        Assert.DoesNotContain("unreviewed", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("provisional", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidence", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("schema", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostRunRequest_StoresUnreviewedProvisionalRequest()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/run-requests", new WideRunRequest
        {
            RiverName = "Little River",
            PutInText = "Old bridge",
            TakeOutText = "Mill landing",
            SourceHints = "county map"
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WideRunRequestSubmissionResult>();
        var stored = result?.Request;

        Assert.NotNull(stored);
        Assert.Equal("Little River", stored.RiverName);
        Assert.Equal("unreviewed", stored.ReviewStatus);
        Assert.Equal("provisional", stored.VerificationStatus);
        Assert.Equal("public_request", stored.Source);
        Assert.NotEqual(stored.PutInRawAccessPointCandidateId, stored.TakeOutRawAccessPointCandidateId);
    }

    [Fact]
    public async Task PostRunRequest_ReturnsImmediateBestAvailableEstimate_WhenRiverIsGrounded()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/run-requests", new WideRunRequest
        {
            RiverName = "Neuse River",
            PutInText = "Falls Dam",
            TakeOutText = "Buffaloe Road"
        });

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<WideRunRequestSubmissionResult>();

        Assert.NotNull(result);
        Assert.NotNull(result.Estimate);
        Assert.True(result.EstimateBasis.CanEstimate);
        Assert.Equal("grounded_river_mile_delta", result.EstimateBasis.DistanceBasis);
        Assert.Equal(10.45, result.Estimate.DistanceMiles);
        Assert.Equal("provisional_estimate", result.EstimateBasis.Status);
        Assert.Null(result.EstimateBasis.MatchedSegmentId);
        Assert.Contains("needs_run_review", result.EstimateBasis.ReviewFlags);
        Assert.Equal("provisional", result.Request.VerificationStatus);
    }

    [Fact]
    public async Task PostRunRequest_AllowsDuplicates()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var request = new WideRunRequest
        {
            RiverName = "Little River",
            PutInText = "bridge",
            TakeOutText = "landing"
        };

        var first = await client.PostAsJsonAsync("/api/run-requests", request);
        var second = await client.PostAsJsonAsync("/api/run-requests", request);
        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();

        var firstResult = await first.Content.ReadFromJsonAsync<WideRunRequestSubmissionResult>();
        var secondResult = await second.Content.ReadFromJsonAsync<WideRunRequestSubmissionResult>();

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.NotEqual(firstResult.Request.RunId, secondResult.Request.RunId);
    }

    [Fact]
    public async Task PostRunRequest_DoesNotReturnEstimate_WhenRiverIsUngrounded()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/run-requests", new WideRunRequest
        {
            RiverName = "Unlisted River",
            PutInText = "bridge",
            TakeOutText = "landing"
        });
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<WideRunRequestSubmissionResult>();

        Assert.NotNull(result);
        Assert.Null(result.Estimate);
        Assert.False(result.EstimateBasis.CanEstimate);
        Assert.Equal("ungrounded", result.EstimateBasis.Status);
        Assert.Contains("needs_river_review", result.EstimateBasis.ReviewFlags);
    }

    [Fact]
    public async Task RequestRunPage_DoesNotRedirect()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/request-run");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configurationBuilder) =>
                {
                    configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:RiverSignals"] = connectionString
                    });
                });
            });
    }

    private sealed class SqliteTestDatabase : IDisposable
    {
        private readonly string _directoryPath;

        private SqliteTestDatabase(string directoryPath, string databasePath)
        {
            _directoryPath = directoryPath;
            ConnectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        }

        public string ConnectionString { get; }

        public static SqliteTestDatabase Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), $"wide-run-request-api-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "riversignals.db");
            return new SqliteTestDatabase(directoryPath, databasePath);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(_directoryPath))
            {
                Directory.Delete(_directoryPath, recursive: true);
            }
        }
    }
}
