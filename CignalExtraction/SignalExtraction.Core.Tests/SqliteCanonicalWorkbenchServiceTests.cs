using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class SqliteCanonicalWorkbenchServiceTests
{
    [Fact]
    public void GetTables_ReturnsMissionCanonicalTables_WithEditableFields()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteCanonicalWorkbenchService(database.ConnectionString);

        var tables = service.GetTables();

        Assert.Contains(tables, table => table.Name == "runs" && table.EditableFields.Contains("review_status"));
        Assert.Contains(tables, table => table.Name == "access_points" && table.EditableFields.Contains("river_mile_confidence"));
        Assert.Contains(tables, table => table.Name == "raw_access_point_candidates" && table.EditableFields.Contains("raw_text"));
        Assert.Contains(tables, table => table.Name == "access_point_identifiers" && table.EditableFields.Contains("identifier_value"));
        Assert.Contains(tables, table => table.Name == "completed_run_observations" && table.EditableFields.Contains("pipeline_stage"));
        Assert.Contains(tables, table => table.Name == "gauges" && table.EditableFields.Contains("river_mile_source"));
        Assert.Contains(tables, table => table.Name == "run_gauge_links" && table.EditableFields.Contains("relationship"));
    }

    [Fact]
    public void UpdateRecord_PersistsExplicitEditableFieldChanges()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteCanonicalWorkbenchService(database.ConnectionString);
        InsertRun(database.ConnectionString, "run-1", "Neuse River", "unreviewed", "before");

        var updated = service.UpdateRecord(
            "runs",
            "run-1",
            new Dictionary<string, string?>
            {
                ["review_status"] = "accepted",
                ["notes"] = "Reviewed by human truth editor",
                ["id"] = "not-allowed"
            });

        Assert.NotNull(updated);
        Assert.Equal("run-1", updated.Id);
        Assert.Equal("accepted", updated.Values["review_status"]);
        Assert.Equal("Reviewed by human truth editor", updated.Values["notes"]);
        Assert.NotNull(updated.Values["updated_at_utc"]);
        Assert.Equal(("accepted", "Reviewed by human truth editor"), QueryRunReview(database.ConnectionString, "run-1"));
    }

    [Fact]
    public void GetRecords_PreservesDuplicateAndAmbiguousRecords()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteCanonicalWorkbenchService(database.ConnectionString);
        InsertAccessPoint(database.ConnectionString, "access-1", "Neuse River", "River Access");
        InsertAccessPoint(database.ConnectionString, "access-2", "Neuse River", "River Access");

        var records = service.GetRecords("access_points");

        Assert.Equal(2, records.Count);
        Assert.Contains(records, record => record.Id == "access-1" && record.Values["name"] == "River Access");
        Assert.Contains(records, record => record.Id == "access-2" && record.Values["name"] == "River Access");
    }

    [Fact]
    public void UpdateRecord_RejectsUnknownTable()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteCanonicalWorkbenchService(database.ConnectionString);

        var exception = Assert.Throws<ArgumentException>(() =>
            service.GetRecords("not_a_canonical_table"));

        Assert.Contains("Unknown canonical workbench table", exception.Message);
    }

    private static void InsertRun(
        string connectionString,
        string id,
        string riverName,
        string reviewStatus,
        string notes)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO runs (id, river_name, review_status, notes)
            VALUES ($id, $riverName, $reviewStatus, $notes);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$riverName", riverName);
        command.Parameters.AddWithValue("$reviewStatus", reviewStatus);
        command.Parameters.AddWithValue("$notes", notes);
        command.ExecuteNonQuery();
    }

    private static void InsertAccessPoint(
        string connectionString,
        string id,
        string riverName,
        string name)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO access_points (id, river_name, name)
            VALUES ($id, $riverName, $name);
            """;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$riverName", riverName);
        command.Parameters.AddWithValue("$name", name);
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
            var directoryPath = Path.Combine(Path.GetTempPath(), $"canonical-workbench-sqlite-{Guid.NewGuid():N}");
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
