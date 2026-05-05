using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class SqliteFlowReadingService : IFlowReadingService
{
    private readonly string _connectionString;

    public SqliteFlowReadingService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQLite connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
        EnsureDatabase();
    }

    public void AddFlowReading(FlowReading reading)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FlowReadings (
                Id,
                SegmentId,
                ObservedAtUtc,
                EstimatedCurrentMph,
                GaugeHeightFeet,
                FlowRateCfs,
                Source,
                SourceReference,
                CreatedAtUtc
            )
            VALUES (
                $id,
                $segmentId,
                $observedAtUtc,
                $estimatedCurrentMph,
                $gaugeHeightFeet,
                $flowRateCfs,
                $source,
                $sourceReference,
                $createdAtUtc
            );
            """;

        AddCommonParameters(command, reading);
        command.Parameters.AddWithValue("$createdAtUtc", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public FlowReading? GetLatestForSegment(Guid segmentId)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                Id,
                SegmentId,
                ObservedAtUtc,
                EstimatedCurrentMph,
                GaugeHeightFeet,
                FlowRateCfs,
                Source,
                SourceReference
            FROM FlowReadings
            WHERE SegmentId = $segmentId
            ORDER BY ObservedAtUtc DESC, CreatedAtUtc DESC, Id DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$segmentId", segmentId.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        return MapReading(reader);
    }

    private void EnsureDatabase()
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS FlowReadings (
                Id TEXT PRIMARY KEY NOT NULL,
                SegmentId TEXT NOT NULL,
                ObservedAtUtc TEXT NOT NULL,
                EstimatedCurrentMph REAL NULL,
                GaugeHeightFeet REAL NULL,
                FlowRateCfs REAL NULL,
                Source TEXT NOT NULL,
                SourceReference TEXT NULL,
                CreatedAtUtc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_FlowReadings_SegmentId
                ON FlowReadings (SegmentId);

            CREATE INDEX IF NOT EXISTS IX_FlowReadings_SegmentId_ObservedAtUtc
                ON FlowReadings (SegmentId, ObservedAtUtc);
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection CreateOpenConnection()
    {
        return SqliteConnectionFactory.CreateOpenConnection(_connectionString);
    }

    private static void AddCommonParameters(SqliteCommand command, FlowReading reading)
    {
        command.Parameters.AddWithValue("$id", reading.Id.ToString());
        command.Parameters.AddWithValue("$segmentId", reading.SegmentId.ToString());
        command.Parameters.AddWithValue("$observedAtUtc", reading.ObservedAtUtc.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$estimatedCurrentMph", ToDbValue(reading.EstimatedCurrentMph));
        command.Parameters.AddWithValue("$gaugeHeightFeet", ToDbValue(reading.GaugeHeightFeet));
        command.Parameters.AddWithValue("$flowRateCfs", ToDbValue(reading.FlowRateCfs));
        command.Parameters.AddWithValue("$source", reading.Source);
        command.Parameters.AddWithValue("$sourceReference", ToDbValue(reading.SourceReference));
    }

    private static object ToDbValue(double? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static FlowReading MapReading(SqliteDataReader reader)
    {
        return new FlowReading
        {
            Id = Guid.Parse(reader.GetString(0)),
            SegmentId = Guid.Parse(reader.GetString(1)),
            ObservedAtUtc = DateTime.Parse(reader.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
            EstimatedCurrentMph = reader.IsDBNull(3) ? null : reader.GetDouble(3),
            GaugeHeightFeet = reader.IsDBNull(4) ? null : reader.GetDouble(4),
            FlowRateCfs = reader.IsDBNull(5) ? null : reader.GetDouble(5),
            Source = reader.GetString(6),
            SourceReference = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }
}
