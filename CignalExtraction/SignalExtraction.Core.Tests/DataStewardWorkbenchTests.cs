using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class DataStewardWorkbenchTests
{
    [Fact]
    public async Task InternalDataStewardPage_ReturnsWorkbenchSurface()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/internal/data-steward");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);

        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<title>Data Steward Workbench</title>", html);
        Assert.Contains("Internal stewardship for canonical and provisional RiverSignals records.", html);
        Assert.Contains("not automatically trusted, promoted, reconciled, deduplicated, or used by estimates", html);
        Assert.Contains("runs", html);
        Assert.Contains("access_points", html);
        Assert.Contains("completed_run_observations", html);
        Assert.DoesNotContain("/api/segments/${segmentId}/estimate", html);
    }

    [Fact]
    public async Task InternalDataStewardApi_AllowsExplicitEditPersistence()
    {
        using var database = SqliteTestDatabase.Create();
        new SqliteCanonicalSchemaInitializer(database.ConnectionString).EnsureCreated();
        InsertRun(database.ConnectionString, "run-1", "Neuse River");
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        var updateResponse = await client.PutAsJsonAsync(
            "/api/internal/data-steward/runs/run-1",
            new CanonicalWorkbenchUpdateRequest
            {
                Values = new Dictionary<string, string?>
                {
                    ["review_status"] = "accepted",
                    ["source"] = "personal",
                    ["confidence"] = "0.8",
                    ["notes"] = "human-reviewed workbench edit"
                }
            });

        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<CanonicalWorkbenchRecord>();

        Assert.NotNull(updated);
        Assert.Equal("accepted", updated.Values["review_status"]);
        Assert.Equal("personal", updated.Values["source"]);
        Assert.Equal("0.8", updated.Values["confidence"]);
        Assert.Equal("human-reviewed workbench edit", QueryRunNotes(database.ConnectionString, "run-1"));
    }

    [Fact]
    public async Task InternalDataStewardApi_PreservesDuplicateAmbiguousRecords()
    {
        using var database = SqliteTestDatabase.Create();
        new SqliteCanonicalSchemaInitializer(database.ConnectionString).EnsureCreated();
        InsertRun(database.ConnectionString, "run-1", "Neuse River");
        InsertRun(database.ConnectionString, "run-2", "Neuse River");
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();

        var records = await client.GetFromJsonAsync<List<CanonicalWorkbenchRecord>>("/api/internal/data-steward/runs");

        Assert.NotNull(records);
        Assert.True(records.Count >= 2);
        Assert.Contains(records, record => record.Id == "run-1" && record.Values["river_name"] == "Neuse River");
        Assert.Contains(records, record => record.Id == "run-2" && record.Values["river_name"] == "Neuse River");
    }

    [Fact]
    public async Task InternalDataStewardEdit_DoesNotChangeSeededEstimateOutput()
    {
        using var database = SqliteTestDatabase.Create();
        new SqliteCanonicalSchemaInitializer(database.ConnectionString).EnsureCreated();
        InsertRun(database.ConnectionString, "run-1", "Neuse River");
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient();
        var segmentId = "55555555-5555-5555-5555-111111111111";

        var before = await EstimateSeededRun(client, segmentId);

        var updateResponse = await client.PutAsJsonAsync(
            "/api/internal/data-steward/runs/run-1",
            new CanonicalWorkbenchUpdateRequest
            {
                Values = new Dictionary<string, string?>
                {
                    ["review_status"] = "accepted",
                    ["distance_miles"] = "99",
                    ["notes"] = "canonical edit must not influence seeded estimator"
                }
            });
        updateResponse.EnsureSuccessStatusCode();

        var after = await EstimateSeededRun(client, segmentId);

        Assert.Equal(before.DistanceMiles, after.DistanceMiles);
        Assert.Equal(before.RiverCurrentMphUsed, after.RiverCurrentMphUsed);
        Assert.Equal(before.EffectiveSpeedMph, after.EffectiveSpeedMph);
        Assert.Equal(before.EstimatedDuration, after.EstimatedDuration);
    }

    [Fact]
    public async Task InternalDataStewardPage_DoesNotRedirect()
    {
        using var database = SqliteTestDatabase.Create();
        await using var factory = CreateFactory(database.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/internal/data-steward");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<SeededEstimateSnapshot> EstimateSeededRun(HttpClient client, string segmentId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/segments/{segmentId}/estimate",
            new { paddlingSpeedMph = 3.0 });
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var segment = root.GetProperty("segment");
        var estimate = root.GetProperty("estimate");

        return new SeededEstimateSnapshot(
            segment.GetProperty("distanceMiles").GetDouble(),
            estimate.GetProperty("riverCurrentMphUsed").GetDouble(),
            estimate.GetProperty("effectiveSpeedMph").GetDouble(),
            estimate.GetProperty("estimatedDuration").GetString() ?? string.Empty);
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

    private static void InsertRun(string connectionString, string id, string riverName)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO runs (id, river_name)
            VALUES ($id, $riverName);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$riverName", riverName);
        command.ExecuteNonQuery();
    }

    private static string QueryRunNotes(string connectionString, string id)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT notes FROM runs WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return reader.GetString(0);
    }

    private sealed record SeededEstimateSnapshot(
        double DistanceMiles,
        double RiverCurrentMphUsed,
        double EffectiveSpeedMph,
        string EstimatedDuration);

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
            var directoryPath = Path.Combine(Path.GetTempPath(), $"data-steward-workbench-{Guid.NewGuid():N}");
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
