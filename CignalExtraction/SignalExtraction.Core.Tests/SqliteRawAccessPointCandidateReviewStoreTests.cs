using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;
using SignalExtraction.Core.Services;

namespace SignalExtraction.Core.Tests;

public class SqliteRawAccessPointCandidateReviewStoreTests
{
    [Fact]
    public void ReviewMetadata_PersistsAcrossStoreRecreation()
    {
        using var database = SqliteTestDatabase.Create();
        var candidateId = new Guid("a1000000-0000-0000-0000-000000000002");

        var writer = new SqliteRawAccessPointCandidateReviewStore(database.ConnectionString);
        writer.UpsertReviewMetadata(
            candidateId,
            RawAccessPointReviewState.PromotableLater,
            "source is ready for later review");

        var reader = new SqliteRawAccessPointCandidateReviewStore(database.ConnectionString);
        var reviewMetadata = reader.GetReviewMetadata();

        Assert.True(reviewMetadata.TryGetValue(candidateId, out var metadata));
        Assert.Equal(candidateId, metadata.CandidateId);
        Assert.Equal(RawAccessPointReviewState.PromotableLater, metadata.ReviewState);
        Assert.Equal("source is ready for later review", metadata.ReviewerNote);
    }

    [Fact]
    public void Constructor_CreatesDatabaseDirectory_WhenMissing()
    {
        using var database = SqliteTestDatabase.CreateWithoutDirectory();
        var candidateId = new Guid("a1000000-0000-0000-0000-000000000002");

        var writer = new SqliteRawAccessPointCandidateReviewStore(database.ConnectionString);
        writer.UpsertReviewMetadata(
            candidateId,
            RawAccessPointReviewState.NeedsMoreSourceContext,
            "directory creation check");

        var reader = new SqliteRawAccessPointCandidateReviewStore(database.ConnectionString);
        var reviewMetadata = reader.GetReviewMetadata();

        Assert.True(Directory.Exists(database.DirectoryPath));
        Assert.True(reviewMetadata.TryGetValue(candidateId, out var metadata));
        Assert.Equal(RawAccessPointReviewState.NeedsMoreSourceContext, metadata.ReviewState);
    }

    [Fact]
    public void RawAccessPointCandidateService_LoadsPersistedReviewMetadata()
    {
        using var database = SqliteTestDatabase.Create();
        var candidateId = new Guid("a1000000-0000-0000-0000-000000000002");
        var writerStore = new SqliteRawAccessPointCandidateReviewStore(database.ConnectionString);
        var writer = new RawAccessPointCandidateService(writerStore);

        writer.UpdateReviewState(
            candidateId,
            RawAccessPointReviewState.DuplicateCandidate,
            "  same source record in another list  ");

        var readerStore = new SqliteRawAccessPointCandidateReviewStore(database.ConnectionString);
        var reader = new RawAccessPointCandidateService(readerStore);
        var candidate = Assert.Single(
            reader.GetRawAccessPointCandidates(),
            candidate => candidate.Id == candidateId);

        Assert.Equal(RawAccessPointReviewState.DuplicateCandidate, candidate.ReviewState);
        Assert.Equal("same source record in another list", candidate.ReviewerNote);
        Assert.False(candidate.IsResolved);
    }

    [Fact]
    public void PersistedReviewMetadata_DoesNotPromoteCandidatesIntoCatalogEntities()
    {
        using var database = SqliteTestDatabase.Create();
        var store = new SqliteRawAccessPointCandidateReviewStore(database.ConnectionString);
        var rawService = new RawAccessPointCandidateService(store);
        var catalogService = new SegmentCatalogService();
        var candidateId = rawService.GetRawAccessPointCandidates().First().Id;

        rawService.UpdateReviewState(
            candidateId,
            RawAccessPointReviewState.LikelyExistingAccessPoint,
            "review metadata only");

        var reloadedRawService = new RawAccessPointCandidateService(
            new SqliteRawAccessPointCandidateReviewStore(database.ConnectionString));
        var rawCandidateIds = reloadedRawService.GetRawAccessPointCandidates().Select(candidate => candidate.Id).ToHashSet();
        var accessPointIds = catalogService.GetPresetAccessPoints().Select(accessPoint => accessPoint.Id).ToHashSet();
        var segmentIds = catalogService.GetPresetSegments().Select(segment => segment.Id).ToHashSet();

        Assert.Empty(rawCandidateIds.Intersect(accessPointIds));
        Assert.Empty(rawCandidateIds.Intersect(segmentIds));
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
            var directoryPath = Path.Combine(Path.GetTempPath(), $"raw-access-review-sqlite-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            var databasePath = Path.Combine(directoryPath, "raw-access-review.db");
            return new SqliteTestDatabase(directoryPath, databasePath);
        }

        public static SqliteTestDatabase CreateWithoutDirectory()
        {
            var directoryPath = Path.Combine(Path.GetTempPath(), $"raw-access-review-sqlite-missing-{Guid.NewGuid():N}");
            var databasePath = Path.Combine(directoryPath, "raw-access-review.db");
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
