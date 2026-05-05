using Microsoft.Data.Sqlite;
using SignalExtraction.Core.Models;

namespace SignalExtraction.Core.Services;

public class SqliteSeededCanonicalCatalogSeeder
{
    private const string LegacySeedSource = "legacy_seed";
    private const string SeededCatalogReference = "SegmentCatalogService";
    private const double SeededCatalogConfidence = 0.7;

    private readonly string _connectionString;
    private readonly ISegmentCatalogService _segmentCatalogService;

    public SqliteSeededCanonicalCatalogSeeder(
        string connectionString,
        ISegmentCatalogService segmentCatalogService)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQLite connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
        _segmentCatalogService = segmentCatalogService;
    }

    public SeededCanonicalCatalogSeedResult SeedMissing()
    {
        new SqliteCanonicalSchemaInitializer(_connectionString).EnsureCreated();

        using var connection = SqliteConnectionFactory.CreateOpenConnection(_connectionString);
        using var transaction = connection.BeginTransaction();

        var riversById = _segmentCatalogService.GetPresetRivers()
            .ToDictionary(river => river.Id, river => river.Name);
        var accessPoints = _segmentCatalogService.GetPresetAccessPoints();
        var segments = _segmentCatalogService.GetPresetSegments();
        var gauges = _segmentCatalogService.GetPresetUsgsGauges();
        var gaugeTargets = _segmentCatalogService.GetPresetUsgsGaugeImportTargets();

        var accessPointsInserted = accessPoints.Sum(accessPoint =>
            InsertAccessPoint(connection, transaction, accessPoint, GetRiverName(riversById, accessPoint.RiverId)));
        var accessPointIdentifiersInserted = accessPoints.Sum(accessPoint =>
            InsertAccessPointIdentifier(connection, transaction, accessPoint));
        var runsInserted = segments.Sum(segment =>
            InsertRun(connection, transaction, segment, GetRiverName(riversById, segment.RiverId)));
        var gaugesInserted = gauges.Sum(gauge =>
            InsertGauge(connection, transaction, gauge, GetRiverName(riversById, gauge.RiverId)));
        var runGaugeLinksInserted = gaugeTargets.Sum(target =>
            InsertRunGaugeLink(connection, transaction, target));

        transaction.Commit();

        return new SeededCanonicalCatalogSeedResult(
            runsInserted,
            accessPointsInserted,
            accessPointIdentifiersInserted,
            gaugesInserted,
            runGaugeLinksInserted);
    }

    private static int InsertRun(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Segment segment,
        string riverName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO runs (
                id,
                river_name,
                put_in_access_point_id,
                take_out_access_point_id,
                put_in_text,
                take_out_text,
                distance_miles,
                distance_source,
                verification_status,
                review_status,
                source,
                source_reference,
                confidence,
                notes
            )
            VALUES (
                $id,
                $riverName,
                $putInAccessPointId,
                $takeOutAccessPointId,
                $putInText,
                $takeOutText,
                $distanceMiles,
                $distanceSource,
                'source_seeded',
                'provisional',
                $source,
                $sourceReference,
                $confidence,
                $notes
            );
            """;

        command.Parameters.AddWithValue("$id", segment.Id.ToString());
        command.Parameters.AddWithValue("$riverName", riverName);
        command.Parameters.AddWithValue("$putInAccessPointId", segment.StartAccessPointId.ToString());
        command.Parameters.AddWithValue("$takeOutAccessPointId", segment.EndAccessPointId.ToString());
        command.Parameters.AddWithValue("$putInText", ToDbValue(segment.PutInName));
        command.Parameters.AddWithValue("$takeOutText", ToDbValue(segment.TakeOutName));
        command.Parameters.AddWithValue("$distanceMiles", segment.DistanceMiles);
        command.Parameters.AddWithValue("$distanceSource", ToDbValue(segment.DistanceSource ?? segment.PlanningSource));
        command.Parameters.AddWithValue("$source", ToDbValue(segment.PlanningSource) is DBNull ? LegacySeedSource : "source_seeded");
        command.Parameters.AddWithValue("$sourceReference", ToDbValue(segment.PlanningSource ?? SeededCatalogReference));
        command.Parameters.AddWithValue("$confidence", SeededCatalogConfidence);
        command.Parameters.AddWithValue("$notes", ToDbValue(BuildRunNotes(segment)));

        return command.ExecuteNonQuery();
    }

    private static int InsertAccessPoint(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AccessPoint accessPoint,
        string riverName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO access_points (
                id,
                river_name,
                name,
                latitude,
                longitude,
                public_access_status,
                review_status,
                source,
                source_reference,
                confidence,
                river_mile,
                river_mile_source,
                river_mile_confidence,
                notes
            )
            VALUES (
                $id,
                $riverName,
                $name,
                $latitude,
                $longitude,
                $publicAccessStatus,
                'provisional',
                $source,
                $sourceReference,
                $confidence,
                $riverMile,
                $riverMileSource,
                $riverMileConfidence,
                $notes
            );
            """;

        command.Parameters.AddWithValue("$id", accessPoint.Id.ToString());
        command.Parameters.AddWithValue("$riverName", riverName);
        command.Parameters.AddWithValue("$name", accessPoint.Name);
        command.Parameters.AddWithValue("$latitude", ToDbValue(accessPoint.Latitude));
        command.Parameters.AddWithValue("$longitude", ToDbValue(accessPoint.Longitude));
        command.Parameters.AddWithValue("$publicAccessStatus", accessPoint.IsPublic ? "confirmed" : "unknown");
        command.Parameters.AddWithValue("$source", ToDbValue(accessPoint.SourceName) is DBNull ? LegacySeedSource : "source_seeded");
        command.Parameters.AddWithValue("$sourceReference", ToDbValue(accessPoint.SourceName ?? accessPoint.ReviewTrigger ?? SeededCatalogReference));
        command.Parameters.AddWithValue("$confidence", ToDbValue(accessPoint.RiverMileConfidence ?? SeededCatalogConfidence));
        command.Parameters.AddWithValue("$riverMile", ToDbValue(accessPoint.RiverMile));
        command.Parameters.AddWithValue("$riverMileSource", ToDbValue(accessPoint.RiverMileSource));
        command.Parameters.AddWithValue("$riverMileConfidence", ToDbValue(accessPoint.RiverMileConfidence));
        command.Parameters.AddWithValue("$notes", ToDbValue(BuildAccessPointNotes(accessPoint)));

        return command.ExecuteNonQuery();
    }

    private static int InsertAccessPointIdentifier(
        SqliteConnection connection,
        SqliteTransaction transaction,
        AccessPoint accessPoint)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO access_point_identifiers (
                id,
                access_point_id,
                identifier_type,
                identifier_value,
                source,
                source_reference,
                confidence,
                notes
            )
            VALUES (
                $id,
                $accessPointId,
                'primary_name',
                $identifierValue,
                $source,
                $sourceReference,
                $confidence,
                $notes
            );
            """;

        command.Parameters.AddWithValue("$id", $"{accessPoint.Id}:primary_name");
        command.Parameters.AddWithValue("$accessPointId", accessPoint.Id.ToString());
        command.Parameters.AddWithValue("$identifierValue", accessPoint.Name);
        command.Parameters.AddWithValue("$source", ToDbValue(accessPoint.SourceName) is DBNull ? LegacySeedSource : "source_seeded");
        command.Parameters.AddWithValue("$sourceReference", ToDbValue(accessPoint.SourceName ?? SeededCatalogReference));
        command.Parameters.AddWithValue("$confidence", ToDbValue(accessPoint.RiverMileConfidence ?? SeededCatalogConfidence));
        command.Parameters.AddWithValue("$notes", "Primary seeded catalog access point name.");

        return command.ExecuteNonQuery();
    }

    private static int InsertGauge(
        SqliteConnection connection,
        SqliteTransaction transaction,
        UsgsGauge gauge,
        string riverName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO gauges (
                id,
                river_name,
                station_id,
                name,
                source,
                source_reference,
                confidence,
                river_mile,
                river_mile_source,
                river_mile_confidence,
                review_status,
                notes
            )
            VALUES (
                $id,
                $riverName,
                $stationId,
                $name,
                $source,
                $sourceReference,
                $confidence,
                $riverMile,
                $riverMileSource,
                $riverMileConfidence,
                'provisional',
                $notes
            );
            """;

        command.Parameters.AddWithValue("$id", gauge.Id.ToString());
        command.Parameters.AddWithValue("$riverName", riverName);
        command.Parameters.AddWithValue("$stationId", ToDbValue(gauge.StationId));
        command.Parameters.AddWithValue("$name", gauge.Name);
        command.Parameters.AddWithValue("$source", ToDbValue(gauge.Source));
        command.Parameters.AddWithValue("$sourceReference", ToDbValue(gauge.SourceReference));
        command.Parameters.AddWithValue("$confidence", ToDbValue(gauge.RiverMileConfidence ?? SeededCatalogConfidence));
        command.Parameters.AddWithValue("$riverMile", ToDbValue(gauge.RiverMile));
        command.Parameters.AddWithValue("$riverMileSource", ToDbValue(gauge.RiverMileSource));
        command.Parameters.AddWithValue("$riverMileConfidence", ToDbValue(gauge.RiverMileConfidence));
        command.Parameters.AddWithValue("$notes", ToDbValue(BuildGaugeNotes(gauge)));

        return command.ExecuteNonQuery();
    }

    private static int InsertRunGaugeLink(
        SqliteConnection connection,
        SqliteTransaction transaction,
        UsgsGaugeImportTarget target)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR IGNORE INTO run_gauge_links (
                id,
                run_id,
                gauge_id,
                relationship,
                source,
                source_reference,
                confidence,
                notes,
                review_status
            )
            VALUES (
                $id,
                $runId,
                $gaugeId,
                'regional_reference',
                'legacy_seed',
                $sourceReference,
                $confidence,
                $notes,
                $reviewStatus
            );
            """;

        command.Parameters.AddWithValue("$id", $"{target.SegmentId}:{target.GaugeId}");
        command.Parameters.AddWithValue("$runId", target.SegmentId.ToString());
        command.Parameters.AddWithValue("$gaugeId", target.GaugeId.ToString());
        command.Parameters.AddWithValue("$sourceReference", target.MappingConfidenceSource ?? SeededCatalogReference);
        command.Parameters.AddWithValue("$confidence", ToDbValue(target.MappingConfidence));
        command.Parameters.AddWithValue("$notes", ToDbValue(BuildRunGaugeLinkNotes(target)));
        command.Parameters.AddWithValue("$reviewStatus", MapGaugeLinkReviewStatus(target.ReviewStatus));

        return command.ExecuteNonQuery();
    }

    private static string GetRiverName(IReadOnlyDictionary<Guid, string> riversById, Guid riverId)
    {
        return riversById.TryGetValue(riverId, out var riverName)
            ? riverName
            : "Unknown seeded river";
    }

    private static string? BuildRunNotes(Segment segment)
    {
        var notes = new List<string>();
        if (!segment.IsActive)
            notes.Add("Seeded catalog run is inactive in public catalog behavior.");
        if (segment.RiverMileDistanceMiles.HasValue)
            notes.Add($"River-mile comparison distance: {segment.RiverMileDistanceMiles.Value:0.###} miles.");

        return notes.Count == 0 ? null : string.Join(" ", notes);
    }

    private static string? BuildAccessPointNotes(AccessPoint accessPoint)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(accessPoint.AccessType))
            notes.Add($"Access type: {accessPoint.AccessType}.");
        if (!string.IsNullOrWhiteSpace(accessPoint.Address))
            notes.Add($"Address: {accessPoint.Address}.");
        if (!string.IsNullOrWhiteSpace(accessPoint.Amenities))
            notes.Add($"Amenities: {accessPoint.Amenities}.");
        if (!string.IsNullOrWhiteSpace(accessPoint.Notes))
            notes.Add(accessPoint.Notes);

        return notes.Count == 0 ? null : string.Join(" ", notes);
    }

    private static string? BuildGaugeNotes(UsgsGauge gauge)
    {
        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(gauge.Notes))
            notes.Add(gauge.Notes);
        if (!string.IsNullOrWhiteSpace(gauge.ReviewTrigger))
            notes.Add($"Review trigger: {gauge.ReviewTrigger}.");

        return notes.Count == 0 ? null : string.Join(" ", notes);
    }

    private static string? BuildRunGaugeLinkNotes(UsgsGaugeImportTarget target)
    {
        var notes = new List<string>
        {
            $"Legacy relationship type: {target.RelationshipType}."
        };

        if (!string.IsNullOrWhiteSpace(target.Notes))
            notes.Add(target.Notes);

        return string.Join(" ", notes);
    }

    private static string MapGaugeLinkReviewStatus(UsgsGaugeLinkageReviewStatus reviewStatus)
    {
        return reviewStatus == UsgsGaugeLinkageReviewStatus.OperatorSeeded
            ? "accepted"
            : "provisional";
    }

    private static object ToDbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
    }

    private static object ToDbValue(double? value)
    {
        return value.HasValue ? value.Value : DBNull.Value;
    }
}
