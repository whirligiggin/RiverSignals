using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class SqliteWideRunRequestServiceTests
{
    [Fact]
    public void Store_PersistsRunAndRawAccessEvidence_AsUnreviewedProvisional()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteWideRunRequestService(database.ConnectionString);

        var stored = service.Store(new WideRunRequest
        {
            RiverName = "Little River",
            PutInText = "Old bridge access",
            TakeOutText = "Mill landing",
            PutInAlias = "the bridge",
            TakeOutAlias = "old mill",
            RoughLocation = "near town",
            SourceHints = "county map",
            Notes = "Friend asked about this run"
        });

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();

        var run = QuerySingle(
            connection,
            "SELECT river_name, put_in_text, take_out_text, verification_status, review_status, source, source_reference, notes FROM runs WHERE id = $id;",
            stored.RunId);
        Assert.Equal("Little River", run[0]);
        Assert.Equal("Old bridge access", run[1]);
        Assert.Equal("Mill landing", run[2]);
        Assert.Equal("provisional", run[3]);
        Assert.Equal("unreviewed", run[4]);
        Assert.Equal("public_request", run[5]);
        Assert.Equal(stored.RunId, run[6]);
        Assert.Contains("Rough location: near town.", run[7]);
        Assert.Contains("Source hints: county map.", run[7]);

        Assert.Equal(2, CountRows(connection, "raw_access_point_candidates", stored.RunId));
        Assert.Equal(4, CountRows(connection, "access_point_identifiers", stored.RunId));
    }

    [Fact]
    public void Store_AllowsDuplicateAmbiguousRequests()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteWideRunRequestService(database.ConnectionString);
        var request = new WideRunRequest
        {
            RiverName = "Little River",
            PutInText = "bridge",
            TakeOutText = "landing"
        };

        var first = service.Store(request);
        var second = service.Store(request);

        Assert.NotEqual(first.RunId, second.RunId);
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal(2, CountRowsBySource(connection, "runs", "public_request"));
        Assert.Equal(4, CountRowsBySource(connection, "raw_access_point_candidates", "public_request"));
    }

    [Fact]
    public void Store_RejectsOnlyTechnicallyInvalidMissingRequiredFields()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteWideRunRequestService(database.ConnectionString);

        var exception = Assert.Throws<ArgumentException>(() => service.Store(new WideRunRequest
        {
            RiverName = "Little River",
            PutInText = "bridge"
        }));

        Assert.Contains("TakeOutText is required", exception.Message);
    }

    [Fact]
    public void Store_DoesNotChangeSeededEstimateOutputs()
    {
        using var database = SqliteTestDatabase.Create();
        var catalogService = new SegmentCatalogService();
        var before = EstimateAllActiveSegments(catalogService);
        var service = new SqliteWideRunRequestService(database.ConnectionString);

        service.Store(new WideRunRequest
        {
            RiverName = "Unlisted River",
            PutInText = "Somewhere",
            TakeOutText = "Somewhere else"
        });

        var after = EstimateAllActiveSegments(catalogService);

        Assert.Equal(before, after);
    }

    private static IReadOnlyList<EstimateSnapshot> EstimateAllActiveSegments(SegmentCatalogService catalogService)
    {
        var estimationService = new TripEstimationService();

        return catalogService.GetPresetSegments()
            .Where(segment => segment.IsActive)
            .OrderBy(segment => segment.Id)
            .Select(segment =>
            {
                var estimate = estimationService.Estimate(new()
                {
                    SegmentId = segment.Id,
                    SegmentName = segment.Name,
                    DistanceMiles = segment.DistanceMiles,
                    PaddlingSpeedMph = 3.0,
                    RiverCurrentMphOverride = segment.DefaultCurrentMph ?? 0
                });

                return new EstimateSnapshot(
                    segment.Id,
                    estimate.DistanceMiles,
                    estimate.RiverCurrentMphUsed,
                    estimate.EffectiveSpeedMph,
                    estimate.EstimatedDuration);
            })
            .ToList();
    }

    private static IReadOnlyList<string> QuerySingle(SqliteConnection connection, string commandText, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        var values = new List<string>();
        for (var index = 0; index < reader.FieldCount; index++)
        {
            values.Add(reader.IsDBNull(index) ? string.Empty : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return values;
    }

    private static int CountRows(SqliteConnection connection, string tableName, string sourceReference)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE source_reference = $sourceReference;";
        command.Parameters.AddWithValue("$sourceReference", sourceReference);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static int CountRowsBySource(SqliteConnection connection, string tableName, string source)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName} WHERE source = $source;";
        command.Parameters.AddWithValue("$source", source);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private sealed record EstimateSnapshot(
        Guid SegmentId,
        double DistanceMiles,
        double RiverCurrentMphUsed,
        double EffectiveSpeedMph,
        TimeSpan EstimatedDuration);

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
            var directoryPath = Path.Combine(Path.GetTempPath(), $"wide-run-request-{Guid.NewGuid():N}");
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
