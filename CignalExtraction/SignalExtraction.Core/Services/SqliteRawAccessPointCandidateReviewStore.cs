using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class SqliteRawAccessPointCandidateReviewStore : IRawAccessPointCandidateReviewStore
{
    private readonly string _connectionString;

    public SqliteRawAccessPointCandidateReviewStore(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQLite connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
        EnsureDatabase();
    }

    public IReadOnlyDictionary<Guid, RawAccessPointCandidateReviewMetadata> GetReviewMetadata()
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT CandidateId, ReviewState, ReviewerNote
            FROM RawAccessPointCandidateReviews;
            """;

        var reviewMetadata = new Dictionary<Guid, RawAccessPointCandidateReviewMetadata>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var candidateId = Guid.Parse(reader.GetString(0));
            var reviewState = Enum.Parse<RawAccessPointReviewState>(reader.GetString(1));
            var reviewerNote = reader.IsDBNull(2) ? null : reader.GetString(2);

            reviewMetadata[candidateId] = new RawAccessPointCandidateReviewMetadata(
                candidateId,
                reviewState,
                reviewerNote);
        }

        return reviewMetadata;
    }

    public void UpsertReviewMetadata(Guid candidateId, RawAccessPointReviewState reviewState, string? reviewerNote)
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO RawAccessPointCandidateReviews (
                CandidateId,
                ReviewState,
                ReviewerNote,
                UpdatedAtUtc
            )
            VALUES (
                $candidateId,
                $reviewState,
                $reviewerNote,
                $updatedAtUtc
            )
            ON CONFLICT(CandidateId) DO UPDATE SET
                ReviewState = excluded.ReviewState,
                ReviewerNote = excluded.ReviewerNote,
                UpdatedAtUtc = excluded.UpdatedAtUtc;
            """;

        command.Parameters.AddWithValue("$candidateId", candidateId.ToString());
        command.Parameters.AddWithValue("$reviewState", reviewState.ToString());
        command.Parameters.AddWithValue("$reviewerNote", ToDbValue(reviewerNote));
        command.Parameters.AddWithValue("$updatedAtUtc", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private void EnsureDatabase()
    {
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS RawAccessPointCandidateReviews (
                CandidateId TEXT PRIMARY KEY NOT NULL,
                ReviewState TEXT NOT NULL,
                ReviewerNote TEXT NULL,
                UpdatedAtUtc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection CreateOpenConnection()
    {
        return SqliteConnectionFactory.CreateOpenConnection(_connectionString);
    }

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }
}
