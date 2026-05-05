using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Tests;

public class RawAccessReviewStorageConfigurationTests
{
    [Fact]
    public async Task RawAccessReviewMetadata_UsesExplicitReviewConnectionString()
    {
        using var database = SqliteTestDatabases.Create();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:FlowReadings"] = database.FlowConnectionString,
                        ["ConnectionStrings:RawAccessReviews"] = database.RawAccessReviewConnectionString
                    });
                });
            });
        using var client = factory.CreateClient();
        var candidateId = new Guid("a1000000-0000-0000-0000-000000000002");

        var response = await client.PutAsJsonAsync($"/api/raw-access-point-candidates/{candidateId}/review", new
        {
            reviewState = RawAccessPointReviewState.NeedsMoreSourceContext,
            reviewerNote = "configured review storage"
        });

        response.EnsureSuccessStatusCode();
        Assert.Equal(1, CountRawAccessReviewRows(database.RawAccessReviewConnectionString));
        Assert.False(TableExists(database.FlowConnectionString, "RawAccessPointCandidateReviews"));
    }

    [Fact]
    public async Task Stores_UseSharedRiverSignalsConnectionString_WhenNamedOverridesAreMissing()
    {
        using var database = SqliteTestDatabases.Create();
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:FlowReadings"] = null,
                        ["ConnectionStrings:RawAccessReviews"] = null,
                        ["ConnectionStrings:RiverSignals"] = database.SharedConnectionString
                    });
                });
            });
        using var client = factory.CreateClient();
        var candidateId = new Guid("a1000000-0000-0000-0000-000000000002");
        var segmentId = new Guid("33333333-3333-3333-3333-333333333333");

        var reviewResponse = await client.PutAsJsonAsync($"/api/raw-access-point-candidates/{candidateId}/review", new
        {
            reviewState = RawAccessPointReviewState.NeedsMoreSourceContext,
            reviewerNote = "shared storage"
        });
        var observationResponse = await client.PostAsJsonAsync($"/api/segments/{segmentId}/observations", new
        {
            startTimeLocal = new DateTime(2026, 4, 24, 9, 0, 0),
            durationMinutes = 95
        });

        reviewResponse.EnsureSuccessStatusCode();
        observationResponse.EnsureSuccessStatusCode();
        Assert.Equal(1, CountRawAccessReviewRows(database.SharedConnectionString));
        Assert.True(TableExists(database.SharedConnectionString, "TripObservations"));
    }

    private static int CountRawAccessReviewRows(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM RawAccessPointCandidateReviews;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static bool TableExists(string connectionString, string tableName)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name = $tableName;
            """;
        command.Parameters.AddWithValue("$tableName", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private sealed class SqliteTestDatabases : IDisposable
    {
        private readonly string _directoryPath;

        private SqliteTestDatabases(string directoryPath, string flowDatabasePath, string rawAccessReviewDatabasePath)
        {
            _directoryPath = directoryPath;
            FlowConnectionString = new SqliteConnectionStringBuilder { DataSource = flowDatabasePath }.ToString();
            RawAccessReviewConnectionString = new SqliteConnectionStringBuilder { DataSource = rawAccessReviewDatabasePath }.ToString();
            SharedConnectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directoryPath, "riversignals.db") }.ToString();
        }

        public string FlowConnectionString { get; }
        public string RawAccessReviewConnectionString { get; }
        public string SharedConnectionString { get; }

        public static SqliteTestDatabases Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), $"raw-access-review-config-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            return new SqliteTestDatabases(
                directoryPath,
                Path.Combine(directoryPath, "flow-readings.db"),
                Path.Combine(directoryPath, "raw-access-reviews.db"));
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
