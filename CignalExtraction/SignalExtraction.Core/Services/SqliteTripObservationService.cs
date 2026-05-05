using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class SqliteTripObservationService : ITripObservationService
{
    private readonly string _connectionString;

    public SqliteTripObservationService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQLite connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
        EnsureDatabase();
    }

    public TripObservation AddObservation(TripObservation observation)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO TripObservations (
                Id,
                SegmentId,
                ReviewState,
                PipelineStage,
                StartTimeLocal,
                FinishTimeLocal,
                DurationMinutes,
                PutInText,
                TakeOutText,
                Notes,
                CreatedAtUtc
            )
            VALUES (
                $id,
                $segmentId,
                $reviewState,
                $pipelineStage,
                $startTimeLocal,
                $finishTimeLocal,
                $durationMinutes,
                $putInText,
                $takeOutText,
                $notes,
                $createdAtUtc
            );
            """;

        command.Parameters.AddWithValue("$id", observation.Id.ToString());
        command.Parameters.AddWithValue("$segmentId", observation.SegmentId.ToString());
        command.Parameters.AddWithValue("$reviewState", observation.ReviewState.ToString());
        command.Parameters.AddWithValue("$pipelineStage", observation.PipelineStage.ToString());
        command.Parameters.AddWithValue("$startTimeLocal", observation.StartTimeLocal.ToString("O"));
        command.Parameters.AddWithValue("$finishTimeLocal", ToDbValue(observation.FinishTimeLocal));
        command.Parameters.AddWithValue("$durationMinutes", ToDbValue(observation.DurationMinutes));
        command.Parameters.AddWithValue("$putInText", ToDbValue(observation.PutInText));
        command.Parameters.AddWithValue("$takeOutText", ToDbValue(observation.TakeOutText));
        command.Parameters.AddWithValue("$notes", ToDbValue(observation.Notes));
        command.Parameters.AddWithValue("$createdAtUtc", observation.CreatedAtUtc.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();

        return observation;
    }

    private void EnsureDatabase()
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS TripObservations (
                Id TEXT PRIMARY KEY NOT NULL,
                SegmentId TEXT NOT NULL,
                ReviewState TEXT NOT NULL DEFAULT 'Unreviewed',
                PipelineStage TEXT NOT NULL DEFAULT 'Structured',
                StartTimeLocal TEXT NOT NULL,
                FinishTimeLocal TEXT NULL,
                DurationMinutes INTEGER NULL,
                PutInText TEXT NULL,
                TakeOutText TEXT NULL,
                Notes TEXT NULL,
                CreatedAtUtc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_TripObservations_SegmentId
                ON TripObservations (SegmentId);
            """;
        command.ExecuteNonQuery();

        using var migrationCommand = connection.CreateCommand();
        migrationCommand.CommandText = """
            ALTER TABLE TripObservations
            ADD COLUMN ReviewState TEXT NOT NULL DEFAULT 'Unreviewed';
            """;

        try
        {
            migrationCommand.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // Existing database already has the column.
        }

        using var pipelineStageMigrationCommand = connection.CreateCommand();
        pipelineStageMigrationCommand.CommandText = """
            ALTER TABLE TripObservations
            ADD COLUMN PipelineStage TEXT NOT NULL DEFAULT 'Structured';
            """;

        try
        {
            pipelineStageMigrationCommand.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // Existing database already has the column.
        }
    }

    private SqliteConnection CreateOpenConnection()
    {
        return SqliteConnectionFactory.CreateOpenConnection(_connectionString);
    }

    private static object ToDbValue(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("O") : DBNull.Value;
    }

    private static object ToDbValue(int? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }
}
