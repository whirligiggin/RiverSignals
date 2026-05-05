using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class SqliteWideRunRequestService : IWideRunRequestService
{
    private const string PublicRequestSource = "public_request";

    private readonly string _connectionString;

    public SqliteWideRunRequestService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQLite connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
        new SqliteCanonicalSchemaInitializer(connectionString).EnsureCreated();
    }

    public StoredWideRunRequest Store(WideRunRequest request)
    {
        var riverName = RequireText(request.RiverName, nameof(request.RiverName));
        var putInText = RequireText(request.PutInText, nameof(request.PutInText));
        var takeOutText = RequireText(request.TakeOutText, nameof(request.TakeOutText));

        var runId = Guid.NewGuid().ToString();
        var putInCandidateId = Guid.NewGuid().ToString();
        var takeOutCandidateId = Guid.NewGuid().ToString();

        using var connection = SqliteConnectionFactory.CreateOpenConnection(_connectionString);
        using var transaction = connection.BeginTransaction();

        InsertRawCandidate(
            connection,
            transaction,
            putInCandidateId,
            riverName,
            putInText,
            request.RoughLocation,
            runId,
            request.Notes);
        InsertRawCandidate(
            connection,
            transaction,
            takeOutCandidateId,
            riverName,
            takeOutText,
            request.RoughLocation,
            runId,
            request.Notes);
        InsertAliasIdentifier(
            connection,
            transaction,
            putInCandidateId,
            request.PutInAlias,
            runId,
            "put_in_alias");
        InsertAliasIdentifier(
            connection,
            transaction,
            takeOutCandidateId,
            request.TakeOutAlias,
            runId,
            "take_out_alias");
        InsertSourceHintIdentifier(
            connection,
            transaction,
            putInCandidateId,
            request.SourceHints,
            runId,
            "put_in_source_hint");
        InsertSourceHintIdentifier(
            connection,
            transaction,
            takeOutCandidateId,
            request.SourceHints,
            runId,
            "take_out_source_hint");
        InsertRun(
            connection,
            transaction,
            runId,
            riverName,
            putInText,
            takeOutText,
            request.RoughLocation,
            request.Notes,
            request.SourceHints);

        transaction.Commit();

        return new StoredWideRunRequest(
            runId,
            riverName,
            putInText,
            takeOutText,
            "unreviewed",
            "provisional",
            PublicRequestSource,
            runId,
            putInCandidateId,
            takeOutCandidateId);
    }

    private static void InsertRun(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string runId,
        string riverName,
        string putInText,
        string takeOutText,
        string? roughLocation,
        string? notes,
        string? sourceHints)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO runs (
                id,
                river_name,
                put_in_text,
                take_out_text,
                verification_status,
                review_status,
                source,
                source_reference,
                notes
            )
            VALUES (
                $id,
                $riverName,
                $putInText,
                $takeOutText,
                'provisional',
                'unreviewed',
                $source,
                $sourceReference,
                $notes
            );
            """;
        command.Parameters.AddWithValue("$id", runId);
        command.Parameters.AddWithValue("$riverName", riverName);
        command.Parameters.AddWithValue("$putInText", putInText);
        command.Parameters.AddWithValue("$takeOutText", takeOutText);
        command.Parameters.AddWithValue("$source", PublicRequestSource);
        command.Parameters.AddWithValue("$sourceReference", runId);
        command.Parameters.AddWithValue("$notes", ToDbValue(BuildNotes(roughLocation, notes, sourceHints)));
        command.ExecuteNonQuery();
    }

    private static void InsertRawCandidate(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string candidateId,
        string riverName,
        string rawText,
        string? roughLocation,
        string runId,
        string? notes)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO raw_access_point_candidates (
                id,
                river_name,
                name,
                raw_text,
                review_status,
                source,
                source_reference,
                notes
            )
            VALUES (
                $id,
                $riverName,
                $name,
                $rawText,
                'unreviewed',
                $source,
                $sourceReference,
                $notes
            );
            """;
        command.Parameters.AddWithValue("$id", candidateId);
        command.Parameters.AddWithValue("$riverName", riverName);
        command.Parameters.AddWithValue("$name", rawText);
        command.Parameters.AddWithValue("$rawText", rawText);
        command.Parameters.AddWithValue("$source", PublicRequestSource);
        command.Parameters.AddWithValue("$sourceReference", runId);
        command.Parameters.AddWithValue("$notes", ToDbValue(BuildNotes(roughLocation, notes, null)));
        command.ExecuteNonQuery();
    }

    private static void InsertAliasIdentifier(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string rawCandidateId,
        string? alias,
        string runId,
        string identifierType)
    {
        if (string.IsNullOrWhiteSpace(alias))
            return;

        InsertIdentifier(connection, transaction, rawCandidateId, alias, runId, identifierType, "Submitted alias for requested access point.");
    }

    private static void InsertSourceHintIdentifier(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string rawCandidateId,
        string? sourceHints,
        string runId,
        string identifierType)
    {
        if (string.IsNullOrWhiteSpace(sourceHints))
            return;

        InsertIdentifier(connection, transaction, rawCandidateId, sourceHints, runId, identifierType, "Submitted source hint for requested run.");
    }

    private static void InsertIdentifier(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string rawCandidateId,
        string value,
        string runId,
        string identifierType,
        string notes)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO access_point_identifiers (
                id,
                raw_access_point_candidate_id,
                identifier_type,
                identifier_value,
                source,
                source_reference,
                notes
            )
            VALUES (
                $id,
                $rawAccessPointCandidateId,
                $identifierType,
                $identifierValue,
                $source,
                $sourceReference,
                $notes
            );
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$rawAccessPointCandidateId", rawCandidateId);
        command.Parameters.AddWithValue("$identifierType", identifierType);
        command.Parameters.AddWithValue("$identifierValue", value.Trim());
        command.Parameters.AddWithValue("$source", PublicRequestSource);
        command.Parameters.AddWithValue("$sourceReference", runId);
        command.Parameters.AddWithValue("$notes", notes);
        command.ExecuteNonQuery();
    }

    private static string RequireText(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required.", name);

        return value.Trim();
    }

    private static string? BuildNotes(string? roughLocation, string? notes, string? sourceHints)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(roughLocation))
            parts.Add($"Rough location: {roughLocation.Trim()}.");
        if (!string.IsNullOrWhiteSpace(sourceHints))
            parts.Add($"Source hints: {sourceHints.Trim()}.");
        if (!string.IsNullOrWhiteSpace(notes))
            parts.Add(notes.Trim());

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }
}
