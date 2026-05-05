using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class SqliteSeededCanonicalCatalogSeederTests
{
    [Fact]
    public void SeedMissing_InsertsCurrentSeededCatalogIntoCanonicalTables()
    {
        using var database = SqliteTestDatabase.Create();
        var catalogService = new SegmentCatalogService();
        var seeder = new SqliteSeededCanonicalCatalogSeeder(database.ConnectionString, catalogService);

        var result = seeder.SeedMissing();

        Assert.Equal(catalogService.GetPresetSegments().Count, result.RunsInserted);
        Assert.Equal(catalogService.GetPresetAccessPoints().Count, result.AccessPointsInserted);
        Assert.Equal(catalogService.GetPresetAccessPoints().Count, result.AccessPointIdentifiersInserted);
        Assert.Equal(catalogService.GetPresetUsgsGauges().Count, result.GaugesInserted);
        Assert.Equal(catalogService.GetPresetUsgsGaugeImportTargets().Count, result.RunGaugeLinksInserted);

        Assert.Equal(catalogService.GetPresetSegments().Count, CountRows(database.ConnectionString, "runs"));
        Assert.Equal(catalogService.GetPresetAccessPoints().Count, CountRows(database.ConnectionString, "access_points"));
        Assert.Equal(catalogService.GetPresetUsgsGauges().Count, CountRows(database.ConnectionString, "gauges"));
        Assert.Equal(catalogService.GetPresetUsgsGaugeImportTargets().Count, CountRows(database.ConnectionString, "run_gauge_links"));
    }

    [Fact]
    public void SeedMissing_PreservesProvenanceStatusConfidenceAndRiverMileFields()
    {
        using var database = SqliteTestDatabase.Create();
        var seeder = new SqliteSeededCanonicalCatalogSeeder(database.ConnectionString, new SegmentCatalogService());

        seeder.SeedMissing();

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();

        var run = QuerySingle(
            connection,
            "SELECT river_name, distance_miles, verification_status, review_status, source_reference FROM runs WHERE id = '55555555-5555-5555-5555-111111111111';");
        Assert.Equal("Neuse River", run[0]);
        Assert.Equal("4.7", run[1]);
        Assert.Equal("source_seeded", run[2]);
        Assert.Equal("provisional", run[3]);
        Assert.Contains("City of Raleigh", run[4]);

        var accessPoint = QuerySingle(
            connection,
            "SELECT public_access_status, river_mile, river_mile_source, river_mile_confidence, review_status FROM access_points WHERE id = '20333333-3333-3333-3333-111111111111';");
        Assert.Equal("confirmed", accessPoint[0]);
        Assert.Equal("0.25", accessPoint[1]);
        Assert.Contains("City of Raleigh", accessPoint[2]);
        Assert.Equal("0.8", accessPoint[3]);
        Assert.Equal("provisional", accessPoint[4]);

        var gauge = QuerySingle(
            connection,
            "SELECT station_id, source, river_mile, river_mile_confidence, review_status FROM gauges WHERE id = '30111111-1111-1111-1111-111111111111';");
        Assert.Equal("02087183", gauge[0]);
        Assert.Equal("USGS", gauge[1]);
        Assert.Equal("0.25", gauge[2]);
        Assert.Equal("0.6", gauge[3]);
        Assert.Equal("provisional", gauge[4]);
    }

    [Fact]
    public void SeedMissing_CreatesExplicitConfidenceBearingRunGaugeLinks()
    {
        using var database = SqliteTestDatabase.Create();
        var seeder = new SqliteSeededCanonicalCatalogSeeder(database.ConnectionString, new SegmentCatalogService());

        seeder.SeedMissing();

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        var link = QuerySingle(
            connection,
            """
            SELECT run_id, gauge_id, relationship, confidence, review_status, notes
            FROM run_gauge_links
            WHERE run_id = '55555555-5555-5555-5555-555555555555'
              AND gauge_id = '30111111-1111-1111-1111-111111111111';
            """);

        Assert.Equal("55555555-5555-5555-5555-555555555555", link[0]);
        Assert.Equal("30111111-1111-1111-1111-111111111111", link[1]);
        Assert.Equal("regional_reference", link[2]);
        Assert.Equal("0.5", link[3]);
        Assert.Equal("provisional", link[4]);
        Assert.Contains("Legacy relationship type: CorridorReference", link[5]);
    }

    [Fact]
    public void SeedMissing_IsIdempotent_AndDoesNotOverwriteExistingCanonicalRows()
    {
        using var database = SqliteTestDatabase.Create();
        new SqliteCanonicalSchemaInitializer(database.ConnectionString).EnsureCreated();
        InsertExistingRun(database.ConnectionString);
        var seeder = new SqliteSeededCanonicalCatalogSeeder(database.ConnectionString, new SegmentCatalogService());

        seeder.SeedMissing();
        var secondResult = seeder.SeedMissing();

        Assert.Equal(0, secondResult.RunsInserted);
        Assert.Equal(0, secondResult.AccessPointsInserted);
        Assert.Equal(0, secondResult.AccessPointIdentifiersInserted);
        Assert.Equal(0, secondResult.GaugesInserted);
        Assert.Equal(0, secondResult.RunGaugeLinksInserted);
        Assert.Equal(
            ("accepted", "human steward edit should survive startup seed"),
            QueryRunReview(database.ConnectionString, "55555555-5555-5555-5555-111111111111"));
    }

    [Fact]
    public void SeedMissing_DoesNotChangeSeededEstimateOutputs()
    {
        using var database = SqliteTestDatabase.Create();
        var catalogService = new SegmentCatalogService();
        var before = EstimateAllActiveSegments(catalogService);

        new SqliteSeededCanonicalCatalogSeeder(database.ConnectionString, catalogService).SeedMissing();

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

    private static int CountRows(string connectionString, string tableName)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static IReadOnlyList<string> QuerySingle(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        var values = new List<string>();
        for (var index = 0; index < reader.FieldCount; index++)
        {
            values.Add(reader.IsDBNull(index) ? string.Empty : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return values;
    }

    private static void InsertExistingRun(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO runs (id, river_name, review_status, notes)
            VALUES (
                '55555555-5555-5555-5555-111111111111',
                'Neuse River',
                'accepted',
                'human steward edit should survive startup seed'
            );
            """;
        command.ExecuteNonQuery();
    }

    private static (string ReviewStatus, string Notes) QueryRunReview(string connectionString, string id)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT review_status, notes FROM runs WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetString(0), reader.GetString(1));
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
            var directoryPath = Path.Combine(Path.GetTempPath(), $"seeded-canonical-catalog-{Guid.NewGuid():N}");
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
