using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class SqliteFlowReadingServiceTests
{
    [Fact]
    public void AddFlowReading_StoresDataForLatestLookup()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteFlowReadingService(database.ConnectionString);
        var segmentId = Guid.NewGuid();

        var reading = NewReading(segmentId, new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc), 2.1);

        service.AddFlowReading(reading);
        var latest = service.GetLatestForSegment(segmentId);

        Assert.NotNull(latest);
        Assert.Equal(reading.Id, latest.Id);
        Assert.Equal(segmentId, latest.SegmentId);
        Assert.Equal(reading.ObservedAtUtc, latest.ObservedAtUtc);
        Assert.Equal(2.1, latest.EstimatedCurrentMph);
        Assert.Equal(reading.FlowRateCfs, latest.FlowRateCfs);
        Assert.Equal(reading.GaugeHeightFeet, latest.GaugeHeightFeet);
        Assert.Equal(reading.Source, latest.Source);
        Assert.Equal(reading.SourceReference, latest.SourceReference);
    }

    [Fact]
    public void GetLatestForSegment_ReturnsNewestObservedAtUtc()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteFlowReadingService(database.ConnectionString);
        var segmentId = Guid.NewGuid();

        service.AddFlowReading(NewReading(segmentId, new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc), 1.1));
        service.AddFlowReading(NewReading(segmentId, new DateTime(2026, 4, 22, 14, 0, 0, DateTimeKind.Utc), 2.6));

        var latest = service.GetLatestForSegment(segmentId);

        Assert.NotNull(latest);
        Assert.Equal(2.6, latest.EstimatedCurrentMph);
    }

    [Fact]
    public void GetLatestForSegment_IgnoresOtherSegments()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteFlowReadingService(database.ConnectionString);
        var segmentId = Guid.NewGuid();

        service.AddFlowReading(NewReading(Guid.NewGuid(), new DateTime(2026, 4, 22, 15, 0, 0, DateTimeKind.Utc), 9.9));
        service.AddFlowReading(NewReading(segmentId, new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc), 1.4));

        var latest = service.GetLatestForSegment(segmentId);

        Assert.NotNull(latest);
        Assert.Equal(segmentId, latest.SegmentId);
        Assert.Equal(1.4, latest.EstimatedCurrentMph);
    }

    [Fact]
    public void GetLatestForSegment_ReturnsNull_WhenMissing()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteFlowReadingService(database.ConnectionString);

        var latest = service.GetLatestForSegment(Guid.NewGuid());

        Assert.Null(latest);
    }

    [Fact]
    public void FlowReadings_PersistAcrossServiceRecreation()
    {
        using var database = SqliteTestDatabase.Create();
        var segmentId = Guid.NewGuid();
        var reading = NewReading(segmentId, new DateTime(2026, 4, 22, 16, 0, 0, DateTimeKind.Utc), 3.2);

        var writer = new SqliteFlowReadingService(database.ConnectionString);
        writer.AddFlowReading(reading);

        var reader = new SqliteFlowReadingService(database.ConnectionString);
        var latest = reader.GetLatestForSegment(segmentId);

        Assert.NotNull(latest);
        Assert.Equal(reading.Id, latest.Id);
        Assert.Equal(3.2, latest.EstimatedCurrentMph);
    }

    [Fact]
    public void Constructor_CreatesDatabaseDirectory_WhenMissing()
    {
        using var database = SqliteTestDatabase.CreateWithoutDirectory();

        var service = new SqliteFlowReadingService(database.ConnectionString);
        var segmentId = Guid.NewGuid();
        service.AddFlowReading(NewReading(segmentId, new DateTime(2026, 4, 22, 16, 0, 0, DateTimeKind.Utc), 3.2));

        var latest = service.GetLatestForSegment(segmentId);

        Assert.True(Directory.Exists(database.DirectoryPath));
        Assert.NotNull(latest);
        Assert.Equal(3.2, latest.EstimatedCurrentMph);
    }

    [Fact]
    public void GetLatestForSegment_UsesCreatedAtTieBreak_WhenObservedAtUtcMatches()
    {
        using var database = SqliteTestDatabase.Create();
        var service = new SqliteFlowReadingService(database.ConnectionString);
        var segmentId = Guid.NewGuid();
        var observedAtUtc = new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc);

        service.AddFlowReading(NewReading(segmentId, observedAtUtc, 1.0));
        Thread.Sleep(20);
        service.AddFlowReading(NewReading(segmentId, observedAtUtc, 2.0));

        var latest = service.GetLatestForSegment(segmentId);

        Assert.NotNull(latest);
        Assert.Equal(2.0, latest.EstimatedCurrentMph);
    }

    [Fact]
    public void GetLatestForSegment_UsesIdTieBreak_WhenObservedAtUtcAndCreatedAtUtcMatch()
    {
        using var database = SqliteTestDatabase.Create();
        _ = new SqliteFlowReadingService(database.ConnectionString);
        var segmentId = Guid.NewGuid();
        var observedAtUtc = new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc).ToString("O");
        var createdAtUtc = new DateTime(2026, 4, 22, 12, 1, 0, DateTimeKind.Utc).ToString("O");
        var lowerId = new Guid("11111111-1111-1111-1111-111111111111");
        var higherId = new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF");

        InsertFlowReading(database.ConnectionString, lowerId, segmentId, observedAtUtc, createdAtUtc, 1.0);
        InsertFlowReading(database.ConnectionString, higherId, segmentId, observedAtUtc, createdAtUtc, 2.0);

        var service = new SqliteFlowReadingService(database.ConnectionString);
        var latest = service.GetLatestForSegment(segmentId);

        Assert.NotNull(latest);
        Assert.Equal(higherId, latest.Id);
        Assert.Equal(2.0, latest.EstimatedCurrentMph);
    }

    private static FlowReading NewReading(Guid segmentId, DateTime observedAtUtc, double currentMph)
    {
        return new FlowReading
        {
            Id = Guid.NewGuid(),
            SegmentId = segmentId,
            ObservedAtUtc = observedAtUtc,
            EstimatedCurrentMph = currentMph,
            GaugeHeightFeet = 4.5,
            FlowRateCfs = 2400,
            Source = "TestGauge",
            SourceReference = "Gauge_12345"
        };
    }

    private static void InsertFlowReading(
        string connectionString,
        Guid id,
        Guid segmentId,
        string observedAtUtc,
        string createdAtUtc,
        double currentMph)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
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
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$segmentId", segmentId.ToString());
        command.Parameters.AddWithValue("$observedAtUtc", observedAtUtc);
        command.Parameters.AddWithValue("$estimatedCurrentMph", currentMph);
        command.Parameters.AddWithValue("$gaugeHeightFeet", 4.5);
        command.Parameters.AddWithValue("$flowRateCfs", 2400);
        command.Parameters.AddWithValue("$source", "TestGauge");
        command.Parameters.AddWithValue("$sourceReference", "Gauge_12345");
        command.Parameters.AddWithValue("$createdAtUtc", createdAtUtc);
        command.ExecuteNonQuery();
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
            var directoryPath = Path.Combine(Path.GetTempPath(), $"flow-sqlite-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "flow-readings.db");
            return new SqliteTestDatabase(directoryPath, databasePath);
        }

        public static SqliteTestDatabase CreateWithoutDirectory()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), $"flow-sqlite-missing-{Guid.NewGuid():N}");
            var databasePath = Path.Combine(directoryPath, "flow-readings.db");
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
