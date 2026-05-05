using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class BestAvailableRunEstimateServiceTests
{
    [Fact]
    public void Estimate_UsesCuratedSeededDistance_WhenRequestedRunMatchesActiveSegment()
    {
        var service = CreateService();
        var stored = CreateStoredRequest(
            riverName: "Haw River",
            putInText: "Bynum",
            takeOutText: "US 64");

        var result = service.Estimate(new WideRunRequest
        {
            RiverName = stored.RiverName,
            PutInText = stored.PutInText,
            TakeOutText = stored.TakeOutText
        }, stored);

        Assert.NotNull(result.Estimate);
        Assert.True(result.EstimateBasis.CanEstimate);
        Assert.Equal("seeded_match_estimate", result.EstimateBasis.Status);
        Assert.Equal("curated_run_distance", result.EstimateBasis.DistanceBasis);
        Assert.Equal(new Guid("11111111-1111-1111-1111-111111111111"), result.EstimateBasis.MatchedSegmentId);
        Assert.Equal(6.0, result.Estimate.DistanceMiles);
        Assert.Equal("Haw River - Bynum River Access to US 64 River Access", result.Estimate.SegmentName);
    }

    [Fact]
    public void Estimate_UsesRiverMileDelta_WhenAccessesAreGroundedButNoActiveSegmentMatches()
    {
        var service = CreateService();
        var stored = CreateStoredRequest(
            riverName: "Neuse River",
            putInText: "Falls Dam",
            takeOutText: "Buffaloe Road");

        var result = service.Estimate(new WideRunRequest
        {
            RiverName = stored.RiverName,
            PutInText = stored.PutInText,
            TakeOutText = stored.TakeOutText
        }, stored);

        Assert.NotNull(result.Estimate);
        Assert.True(result.EstimateBasis.CanEstimate);
        Assert.Equal("provisional_estimate", result.EstimateBasis.Status);
        Assert.Equal("grounded_river_mile_delta", result.EstimateBasis.DistanceBasis);
        Assert.Equal(10.45, result.Estimate.DistanceMiles);
        Assert.Null(result.EstimateBasis.MatchedSegmentId);
        Assert.Contains("needs_run_review", result.EstimateBasis.ReviewFlags);
    }

    [Fact]
    public void Estimate_UsesConservativeDefault_WhenOnlyRiverIsGrounded()
    {
        var service = CreateService();
        var stored = CreateStoredRequest(
            riverName: "Tar River",
            putInText: "Unknown bridge",
            takeOutText: "Unknown landing");

        var result = service.Estimate(new WideRunRequest
        {
            RiverName = stored.RiverName,
            PutInText = stored.PutInText,
            TakeOutText = stored.TakeOutText
        }, stored);

        Assert.NotNull(result.Estimate);
        Assert.True(result.EstimateBasis.CanEstimate);
        Assert.Equal("conservative_default_assumption", result.EstimateBasis.DistanceBasis);
        Assert.Equal(5.0, result.Estimate.DistanceMiles);
        Assert.Contains("needs_put_in_review", result.EstimateBasis.ReviewFlags);
        Assert.Contains("needs_take_out_review", result.EstimateBasis.ReviewFlags);
        Assert.Contains("needs_distance_review", result.EstimateBasis.ReviewFlags);
    }

    [Fact]
    public void Estimate_DoesNotEstimate_WhenRiverCannotBeGrounded()
    {
        var service = CreateService();
        var stored = CreateStoredRequest(
            riverName: "Imaginary River",
            putInText: "Unknown bridge",
            takeOutText: "Unknown landing");

        var result = service.Estimate(new WideRunRequest
        {
            RiverName = stored.RiverName,
            PutInText = stored.PutInText,
            TakeOutText = stored.TakeOutText
        }, stored);

        Assert.Null(result.Estimate);
        Assert.False(result.EstimateBasis.CanEstimate);
        Assert.Equal("ungrounded", result.EstimateBasis.Status);
        Assert.Contains("needs_river_review", result.EstimateBasis.ReviewFlags);
    }

    [Fact]
    public void Estimate_DoesNotPromoteOrRewriteStoredRequestedRun()
    {
        using var database = SqliteTestDatabase.Create();
        var requestService = new SqliteWideRunRequestService(database.ConnectionString);
        var estimateService = CreateService();
        var request = new WideRunRequest
        {
            RiverName = "Neuse River",
            PutInText = "Falls Dam",
            TakeOutText = "Buffaloe Road"
        };
        var stored = requestService.Store(request);

        var result = estimateService.Estimate(request, stored);

        Assert.NotNull(result.Estimate);
        using var connection = new SqliteConnection(database.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT verification_status, review_status, distance_miles, put_in_access_point_id, take_out_access_point_id
            FROM runs
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", stored.RunId);

        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal("provisional", reader.GetString(0));
        Assert.Equal("unreviewed", reader.GetString(1));
        Assert.True(reader.IsDBNull(2));
        Assert.True(reader.IsDBNull(3));
        Assert.True(reader.IsDBNull(4));
    }

    private static BestAvailableRunEstimateService CreateService()
    {
        return new BestAvailableRunEstimateService(
            new SegmentCatalogService(),
            new TripEstimationService());
    }

    private static StoredWideRunRequest CreateStoredRequest(
        string riverName,
        string putInText,
        string takeOutText)
    {
        var runId = Guid.NewGuid().ToString();
        return new StoredWideRunRequest(
            runId,
            riverName,
            putInText,
            takeOutText,
            "unreviewed",
            "provisional",
            "public_request",
            runId,
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());
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
            var directoryPath = Path.Combine(Path.GetTempPath(), $"best-available-estimate-{Guid.NewGuid():N}");
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
