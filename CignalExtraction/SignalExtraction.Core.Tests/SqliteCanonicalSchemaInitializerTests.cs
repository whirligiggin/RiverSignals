using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class SqliteCanonicalSchemaInitializerTests
{
    [Fact]
    public void EnsureCreated_CreatesCanonicalTables_WithRequiredColumns()
    {
        using var database = SqliteTestDatabase.Create();

        new SqliteCanonicalSchemaInitializer(database.ConnectionString).EnsureCreated();

        AssertTableHasColumns(database.ConnectionString, "runs", "id", "river_name", "distance_miles", "verification_status", "review_status", "source", "confidence");
        AssertTableHasColumns(database.ConnectionString, "access_points", "id", "river_name", "name", "public_access_status", "review_status", "source", "confidence", "river_mile", "river_mile_source", "river_mile_confidence");
        AssertTableHasColumns(database.ConnectionString, "raw_access_point_candidates", "id", "raw_text", "review_status", "source", "confidence");
        AssertTableHasColumns(database.ConnectionString, "access_point_identifiers", "id", "access_point_id", "raw_access_point_candidate_id", "identifier_type", "identifier_value", "source", "confidence");
        AssertTableHasColumns(database.ConnectionString, "completed_run_observations", "id", "run_id", "segment_id", "review_status", "pipeline_stage", "source", "confidence");
        AssertTableHasColumns(database.ConnectionString, "gauges", "id", "river_name", "station_id", "name", "source", "confidence", "river_mile", "river_mile_source", "river_mile_confidence", "review_status");
        AssertTableHasColumns(database.ConnectionString, "run_gauge_links", "id", "run_id", "gauge_id", "relationship", "source", "confidence", "review_status");
    }

    [Fact]
    public void EnsureCreated_IsIdempotent()
    {
        using var database = SqliteTestDatabase.Create();
        var initializer = new SqliteCanonicalSchemaInitializer(database.ConnectionString);

        initializer.EnsureCreated();
        initializer.EnsureCreated();

        Assert.True(TableExists(database.ConnectionString, "runs"));
        Assert.True(TableExists(database.ConnectionString, "run_gauge_links"));
    }

    [Fact]
    public void CanonicalTables_DefaultToUnreviewedProvisionalStates()
    {
        using var database = SqliteTestDatabase.Create();
        new SqliteCanonicalSchemaInitializer(database.ConnectionString).EnsureCreated();

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        ExecuteNonQuery(connection, """
            INSERT INTO runs (id, river_name)
            VALUES ('run-1', 'Neuse River');

            INSERT INTO access_points (id, river_name, name)
            VALUES ('access-1', 'Neuse River', 'Example Access');

            INSERT INTO gauges (id, river_name, name)
            VALUES ('gauge-1', 'Neuse River', 'Example Gauge');

            INSERT INTO completed_run_observations (id, start_time_local)
            VALUES ('observation-1', '2026-04-29T09:00:00');

            INSERT INTO run_gauge_links (id, run_id, gauge_id, relationship)
            VALUES ('link-1', 'run-1', 'gauge-1', 'between_run');
            """);

        Assert.Equal(("provisional", "unreviewed", "manual_estimate"), QueryRunDefaults(connection));
        Assert.Equal(("unknown", "unreviewed", "manual_estimate"), QueryAccessPointDefaults(connection));
        Assert.Equal(("unreviewed", "structured", "personal"), QueryObservationDefaults(connection));
        Assert.Equal(("USGS", "unreviewed"), QueryGaugeDefaults(connection));
        Assert.Equal(("between_run", "unreviewed", "manual_estimate"), QueryRunGaugeLinkDefaults(connection));
    }

    private static void AssertTableHasColumns(string connectionString, string tableName, params string[] columnNames)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        var actualColumns = GetColumnNames(connection, tableName);

        foreach (var columnName in columnNames)
        {
            Assert.Contains(columnName, actualColumns);
        }
    }

    private static HashSet<string> GetColumnNames(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";

        var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            columnNames.Add(reader.GetString(1));
        }

        return columnNames;
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

    private static void ExecuteNonQuery(SqliteConnection connection, string commandText)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }

    private static (string VerificationStatus, string ReviewStatus, string Source) QueryRunDefaults(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT verification_status, review_status, source FROM runs WHERE id = 'run-1';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    private static (string PublicAccessStatus, string ReviewStatus, string Source) QueryAccessPointDefaults(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT public_access_status, review_status, source FROM access_points WHERE id = 'access-1';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    private static (string ReviewStatus, string PipelineStage, string Source) QueryObservationDefaults(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT review_status, pipeline_stage, source FROM completed_run_observations WHERE id = 'observation-1';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    private static (string Source, string ReviewStatus) QueryGaugeDefaults(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT source, review_status FROM gauges WHERE id = 'gauge-1';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetString(0), reader.GetString(1));
    }

    private static (string Relationship, string ReviewStatus, string Source) QueryRunGaugeLinkDefaults(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT relationship, review_status, source FROM run_gauge_links WHERE id = 'link-1';";
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
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
            var directoryPath = Path.Combine(Path.GetTempPath(), $"canonical-schema-sqlite-{Guid.NewGuid():N}");
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
