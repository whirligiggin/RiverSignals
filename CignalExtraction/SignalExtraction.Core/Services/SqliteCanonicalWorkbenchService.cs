using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class SqliteCanonicalWorkbenchService : ICanonicalWorkbenchService
{
    private static readonly IReadOnlyList<CanonicalWorkbenchTableDefinition> TableDefinitions =
    [
        new("runs", "Runs",
        [
            "river_name",
            "put_in_text",
            "take_out_text",
            "distance_miles",
            "distance_source",
            "verification_status",
            "review_status",
            "source",
            "source_reference",
            "confidence",
            "notes"
        ]),
        new("access_points", "Access Points",
        [
            "river_name",
            "name",
            "latitude",
            "longitude",
            "public_access_status",
            "review_status",
            "source",
            "source_reference",
            "confidence",
            "river_mile",
            "river_mile_source",
            "river_mile_confidence",
            "notes"
        ]),
        new("raw_access_point_candidates", "Raw Access Point Candidates",
        [
            "river_name",
            "name",
            "raw_text",
            "latitude",
            "longitude",
            "review_status",
            "source",
            "source_reference",
            "confidence",
            "notes"
        ]),
        new("access_point_identifiers", "Access Point Identifiers",
        [
            "access_point_id",
            "raw_access_point_candidate_id",
            "identifier_type",
            "identifier_value",
            "source",
            "source_reference",
            "confidence",
            "notes"
        ]),
        new("completed_run_observations", "Completed Run Observations",
        [
            "run_id",
            "segment_id",
            "river_name",
            "put_in_access_point_id",
            "take_out_access_point_id",
            "put_in_text",
            "take_out_text",
            "start_time_local",
            "finish_time_local",
            "duration_minutes",
            "review_status",
            "pipeline_stage",
            "source",
            "source_reference",
            "confidence",
            "notes"
        ]),
        new("gauges", "Gauges",
        [
            "river_name",
            "station_id",
            "name",
            "source",
            "source_reference",
            "confidence",
            "river_mile",
            "river_mile_source",
            "river_mile_confidence",
            "review_status",
            "notes"
        ]),
        new("run_gauge_links", "Run Gauge Links",
        [
            "run_id",
            "gauge_id",
            "relationship",
            "source",
            "source_reference",
            "confidence",
            "notes",
            "review_status"
        ])
    ];

    private static readonly HashSet<string> TablesWithUpdatedAt =
    [
        "runs",
        "access_points",
        "raw_access_point_candidates",
        "gauges",
        "run_gauge_links"
    ];

    private readonly string _connectionString;

    public SqliteCanonicalWorkbenchService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQLite connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
        new SqliteCanonicalSchemaInitializer(connectionString).EnsureCreated();
    }

    public IReadOnlyList<CanonicalWorkbenchTable> GetTables()
    {
        return TableDefinitions
            .Select(table => new CanonicalWorkbenchTable(table.Name, table.Label, table.EditableFields))
            .ToList();
    }

    public IReadOnlyList<CanonicalWorkbenchRecord> GetRecords(string tableName)
    {
        var table = GetTableDefinition(tableName);
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {table.Name} ORDER BY created_at_utc DESC, id LIMIT 250;";

        var records = new List<CanonicalWorkbenchRecord>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(ReadRecord(table.Name, reader));
        }

        return records;
    }

    public CanonicalWorkbenchRecord? UpdateRecord(
        string tableName,
        string id,
        IReadOnlyDictionary<string, string?> values)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Record id is required.", nameof(id));

        var table = GetTableDefinition(tableName);
        var editableFields = table.EditableFields.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedUpdates = values
            .Where(value => editableFields.Contains(value.Key))
            .ToList();

        if (requestedUpdates.Count == 0)
            throw new ArgumentException("At least one editable field is required.", nameof(values));

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        var assignments = requestedUpdates
            .Select((value, index) => $"{value.Key} = $value{index}")
            .ToList();

        if (TablesWithUpdatedAt.Contains(table.Name))
        {
            assignments.Add("updated_at_utc = $updatedAtUtc");
            command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
        }

        command.CommandText = $"""
            UPDATE {table.Name}
            SET {string.Join(", ", assignments)}
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", id);

        for (var index = 0; index < requestedUpdates.Count; index++)
        {
            command.Parameters.AddWithValue($"$value{index}", ToDbValue(requestedUpdates[index].Value));
        }

        var rowsAffected = command.ExecuteNonQuery();
        return rowsAffected == 0
            ? null
            : GetRecord(connection, table.Name, id);
    }

    private CanonicalWorkbenchRecord? GetRecord(SqliteConnection connection, string tableName, string id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {tableName} WHERE id = $id;";
        command.Parameters.AddWithValue("$id", id);

        using var reader = command.ExecuteReader();
        return reader.Read()
            ? ReadRecord(tableName, reader)
            : null;
    }

    private static CanonicalWorkbenchRecord ReadRecord(string tableName, SqliteDataReader reader)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < reader.FieldCount; index++)
        {
            values[reader.GetName(index)] = reader.IsDBNull(index)
                ? null
                : Convert.ToString(reader.GetValue(index), System.Globalization.CultureInfo.InvariantCulture);
        }

        return new CanonicalWorkbenchRecord(tableName, values["id"] ?? string.Empty, values);
    }

    private static CanonicalWorkbenchTableDefinition GetTableDefinition(string tableName)
    {
        var table = TableDefinitions.FirstOrDefault(
            definition => string.Equals(definition.Name, tableName, StringComparison.OrdinalIgnoreCase));

        return table ?? throw new ArgumentException($"Unknown canonical workbench table '{tableName}'.", nameof(tableName));
    }

    private SqliteConnection CreateOpenConnection()
    {
        return SqliteConnectionFactory.CreateOpenConnection(_connectionString);
    }

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private sealed record CanonicalWorkbenchTableDefinition(
        string Name,
        string Label,
        IReadOnlyList<string> EditableFields);
}
