using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class SqliteTripObservationServiceTests
{
    [Fact]
    public void AddObservation_PersistsStructuredPipelineStage_AndUnreviewedState()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteTripObservationService(database.ConnectionString);
        var observation = new TripObservation
        {
            Id = new Guid("aaaaaaaa-1111-1111-1111-111111111111"),
            SegmentId = new Guid("33333333-3333-3333-3333-333333333333"),
            ReviewState = ObservationReviewState.Unreviewed,
            PipelineStage = ObservationPipelineStage.Structured,
            StartTimeLocal = new DateTime(2026, 4, 24, 9, 0, 0),
            DurationMinutes = 95,
            Notes = "Stored boundary test",
            CreatedAtUtc = new DateTime(2026, 4, 24, 14, 0, 0, DateTimeKind.Utc)
        };

        service.AddObservation(observation);

        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ReviewState, PipelineStage
            FROM TripObservations
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", observation.Id.ToString());

        using var reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("Unreviewed", reader.GetString(0));
        Assert.Equal("Structured", reader.GetString(1));
    }

    [Fact]
    public void AddObservation_PersistsAcrossServiceRecreation()
    {
        using var database = SqliteTestDatabase.Create();
        var observation = NewObservation();

        var writer = new SqliteTripObservationService(database.ConnectionString);
        writer.AddObservation(observation);

        _ = new SqliteTripObservationService(database.ConnectionString);

        AssertPersistedObservation(database.ConnectionString, observation.Id, "Unreviewed", "Structured");
    }

    [Fact]
    public void Constructor_CreatesDatabaseDirectory_WhenMissing()
    {
        using var database = SqliteTestDatabase.CreateWithoutDirectory();
        var observation = NewObservation();

        var service = new SqliteTripObservationService(database.ConnectionString);
        service.AddObservation(observation);

        Assert.True(Directory.Exists(database.DirectoryPath));
        AssertPersistedObservation(database.ConnectionString, observation.Id, "Unreviewed", "Structured");
    }

    private static TripObservation NewObservation()
    {
        return new TripObservation
        {
            Id = Guid.NewGuid(),
            SegmentId = new Guid("33333333-3333-3333-3333-333333333333"),
            ReviewState = ObservationReviewState.Unreviewed,
            PipelineStage = ObservationPipelineStage.Structured,
            StartTimeLocal = new DateTime(2026, 4, 24, 9, 0, 0),
            DurationMinutes = 95,
            Notes = "Stored boundary test",
            CreatedAtUtc = new DateTime(2026, 4, 24, 14, 0, 0, DateTimeKind.Utc)
        };
    }

    private static void AssertPersistedObservation(
        string connectionString,
        Guid observationId,
        string expectedReviewState,
        string expectedPipelineStage)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ReviewState, PipelineStage
            FROM TripObservations
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$id", observationId.ToString());

        using var reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal(expectedReviewState, reader.GetString(0));
        Assert.Equal(expectedPipelineStage, reader.GetString(1));
    }

    private sealed class SqliteTestDatabase : IDisposable
    {
        private readonly string _directoryPath;

        private SqliteTestDatabase(string directoryPath, string databasePath)
        {
            _directoryPath = directoryPath;
            ConnectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
            DirectoryPath = directoryPath;
        }

        public string ConnectionString { get; }
        public string DirectoryPath { get; }

        public static SqliteTestDatabase Create()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), $"trip-observation-sqlite-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "trip-observations.db");
            return new SqliteTestDatabase(directoryPath, databasePath);
        }

        public static SqliteTestDatabase CreateWithoutDirectory()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), $"trip-observation-sqlite-missing-{Guid.NewGuid():N}");
            var databasePath = Path.Combine(directoryPath, "trip-observations.db");
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
