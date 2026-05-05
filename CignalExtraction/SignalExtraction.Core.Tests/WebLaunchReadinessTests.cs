using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Tests;

public class WebLaunchReadinessTests
{
    [Fact]
    public async Task PublicLaunchRoutes_ReturnSmokeSuccess_WithoutInternalNavigation()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var root = await client.GetAsync("/");
        var compareRuns = await client.GetAsync("/compare-runs");
        var requestRun = await client.GetAsync("/request-run");
        var completedRun = await client.GetAsync("/completed-run");
        var segments = await client.GetAsync("/api/segments");

        Assert.Equal(HttpStatusCode.OK, root.StatusCode);
        Assert.Equal(HttpStatusCode.OK, compareRuns.StatusCode);
        Assert.Equal(HttpStatusCode.OK, requestRun.StatusCode);
        Assert.Equal(HttpStatusCode.OK, completedRun.StatusCode);
        Assert.Equal(HttpStatusCode.OK, segments.StatusCode);

        var rootHtml = await root.Content.ReadAsStringAsync();
        var compareHtml = await compareRuns.Content.ReadAsStringAsync();
        var requestHtml = await requestRun.Content.ReadAsStringAsync();
        var completedHtml = await completedRun.Content.ReadAsStringAsync();

        AssertPublicPageBoundary(rootHtml);
        AssertPublicPageBoundary(compareHtml);
        AssertPublicPageBoundary(requestHtml);
        AssertPublicPageBoundary(completedHtml);
        Assert.Contains("Estimate a Paddle", rootHtml);
        Assert.Contains("Compare Runs", compareHtml);
        Assert.Contains("Request a Run", requestHtml);
        Assert.Contains("Report a Completed Run", completedHtml);
    }

    [Fact]
    public async Task InternalRoutes_AreExplicitlySeparatedFromPublicNavigation()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var dataSteward = await client.GetAsync("/internal/data-steward");
        var rawAccessCandidates = await client.GetAsync("/internal/raw-access-candidates");
        var dataStewardTables = await client.GetAsync("/api/internal/data-steward/tables");

        Assert.Equal(HttpStatusCode.OK, dataSteward.StatusCode);
        Assert.Equal(HttpStatusCode.OK, rawAccessCandidates.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dataStewardTables.StatusCode);

        var tables = await dataStewardTables.Content.ReadFromJsonAsync<List<CanonicalWorkbenchTable>>();
        Assert.NotNull(tables);
        Assert.NotEmpty(tables);
    }

    [Fact]
    public async Task LaunchConfiguredDatabase_PersistsPublicSubmissionsAcrossFactoryRestart()
    {
        using var database = SqliteTestDatabase.Create();
        var requestId = string.Empty;

        await using (var firstFactory = CreateFactory(database.ConnectionString))
        {
            using var firstClient = firstFactory.CreateClient();
            var response = await firstClient.PostAsJsonAsync("/api/run-requests", new WideRunRequest
            {
                RiverName = "Neuse River",
                PutInText = "Falls Dam",
                TakeOutText = "Buffaloe Road"
            });

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<WideRunRequestSubmissionResult>();
            Assert.NotNull(result);
            requestId = result.Request.RunId;
        }

        await using (var secondFactory = CreateFactory(database.ConnectionString))
        {
            using var secondClient = secondFactory.CreateClient();
            var recordsResponse = await secondClient.GetAsync("/api/internal/data-steward/runs");
            recordsResponse.EnsureSuccessStatusCode();
            var records = await recordsResponse.Content.ReadFromJsonAsync<List<CanonicalWorkbenchRecord>>();

            Assert.NotNull(records);
            Assert.Contains(records, record => record.Id == requestId);
        }
    }

    [Fact]
    public void LaunchReadinessDocs_RecordConfigBackupSmokeRollbackAndRuntimeHygiene()
    {
        var repoRoot = FindRepositoryRoot();
        var launchDoc = File.ReadAllText(Path.Combine(repoRoot, "CignalExtraction", "docs", "web-launch-readiness.md"));
        var persistenceDoc = File.ReadAllText(Path.Combine(repoRoot, "CignalExtraction", "docs", "persistence.md"));
        var gitignore = File.ReadAllText(Path.Combine(repoRoot, ".gitignore"));

        Assert.Contains("ConnectionStrings__RiverSignals", launchDoc);
        Assert.Contains("Backup expectation", launchDoc);
        Assert.Contains("Restore expectation", launchDoc);
        Assert.Contains("Public Route Smoke Checks", launchDoc);
        Assert.Contains("Internal Route Boundary Checks", launchDoc);
        Assert.Contains("Runtime Artifact Hygiene", launchDoc);
        Assert.Contains("Rollback Notes", launchDoc);
        Assert.Contains("Go / No-Go Recommendation", launchDoc);
        Assert.Contains("CignalExtraction/data/riversignals.db", persistenceDoc);
        Assert.Contains("CignalExtraction/data/*.db", gitignore);
        Assert.Contains("CignalExtraction/SignalExtraction.Api/*.db", gitignore);
        Assert.Contains("tmp-*.log", gitignore);
        Assert.Contains("tmp-*.err.log", gitignore);
    }

    private static void AssertPublicPageBoundary(string html)
    {
        Assert.DoesNotContain("href=\"/internal", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"/api/internal", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data-steward", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-access-candidates", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("canonical", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confidence", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("schema", html, StringComparison.OrdinalIgnoreCase);
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

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "CignalExtraction")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be found.");
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
            var directoryPath = Path.Combine(Path.GetTempPath(), $"web-launch-readiness-{Guid.NewGuid():N}");
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
