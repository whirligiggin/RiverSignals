using Microsoft.Data.Sqlite;

namespace SignalExtraction.Core.Services;

public class SqliteCanonicalSchemaInitializer
{
    private readonly string _connectionString;

    public SqliteCanonicalSchemaInitializer(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQLite connection string is required.", nameof(connectionString));

        _connectionString = connectionString;
    }

    public void EnsureCreated()
    {
        using var connection = SqliteConnectionFactory.CreateOpenConnection(_connectionString);
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS runs (
                id TEXT PRIMARY KEY NOT NULL,
                river_name TEXT NOT NULL,
                put_in_access_point_id TEXT NULL,
                take_out_access_point_id TEXT NULL,
                put_in_text TEXT NULL,
                take_out_text TEXT NULL,
                distance_miles REAL NULL,
                distance_source TEXT NULL,
                verification_status TEXT NOT NULL DEFAULT 'provisional',
                review_status TEXT NOT NULL DEFAULT 'unreviewed',
                source TEXT NOT NULL DEFAULT 'manual_estimate',
                source_reference TEXT NULL,
                confidence REAL NULL,
                notes TEXT NULL,
                created_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                updated_at_utc TEXT NULL,
                CHECK (verification_status IN ('personally_verified', 'source_seeded', 'provisional')),
                CHECK (review_status IN ('unreviewed', 'provisional', 'accepted', 'rejected')),
                CHECK (confidence IS NULL OR (confidence >= 0 AND confidence <= 1))
            );

            CREATE TABLE IF NOT EXISTS access_points (
                id TEXT PRIMARY KEY NOT NULL,
                river_name TEXT NOT NULL,
                name TEXT NOT NULL,
                latitude REAL NULL,
                longitude REAL NULL,
                public_access_status TEXT NOT NULL DEFAULT 'unknown',
                review_status TEXT NOT NULL DEFAULT 'unreviewed',
                source TEXT NOT NULL DEFAULT 'manual_estimate',
                source_reference TEXT NULL,
                confidence REAL NULL,
                river_mile REAL NULL,
                river_mile_source TEXT NULL,
                river_mile_confidence REAL NULL,
                notes TEXT NULL,
                created_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                updated_at_utc TEXT NULL,
                CHECK (public_access_status IN ('confirmed', 'assumed', 'unknown')),
                CHECK (review_status IN ('unreviewed', 'provisional', 'accepted', 'rejected')),
                CHECK (confidence IS NULL OR (confidence >= 0 AND confidence <= 1)),
                CHECK (river_mile_confidence IS NULL OR (river_mile_confidence >= 0 AND river_mile_confidence <= 1))
            );

            CREATE TABLE IF NOT EXISTS raw_access_point_candidates (
                id TEXT PRIMARY KEY NOT NULL,
                river_name TEXT NULL,
                name TEXT NULL,
                raw_text TEXT NOT NULL,
                latitude REAL NULL,
                longitude REAL NULL,
                review_status TEXT NOT NULL DEFAULT 'unreviewed',
                source TEXT NOT NULL DEFAULT 'manual_estimate',
                source_reference TEXT NULL,
                confidence REAL NULL,
                notes TEXT NULL,
                created_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                updated_at_utc TEXT NULL,
                CHECK (review_status IN ('unreviewed', 'provisional', 'accepted', 'rejected')),
                CHECK (confidence IS NULL OR (confidence >= 0 AND confidence <= 1))
            );

            CREATE TABLE IF NOT EXISTS access_point_identifiers (
                id TEXT PRIMARY KEY NOT NULL,
                access_point_id TEXT NULL,
                raw_access_point_candidate_id TEXT NULL,
                identifier_type TEXT NOT NULL,
                identifier_value TEXT NOT NULL,
                source TEXT NOT NULL DEFAULT 'manual_estimate',
                source_reference TEXT NULL,
                confidence REAL NULL,
                notes TEXT NULL,
                created_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                CHECK (confidence IS NULL OR (confidence >= 0 AND confidence <= 1)),
                FOREIGN KEY (access_point_id) REFERENCES access_points (id),
                FOREIGN KEY (raw_access_point_candidate_id) REFERENCES raw_access_point_candidates (id)
            );

            CREATE TABLE IF NOT EXISTS completed_run_observations (
                id TEXT PRIMARY KEY NOT NULL,
                run_id TEXT NULL,
                segment_id TEXT NULL,
                river_name TEXT NULL,
                put_in_access_point_id TEXT NULL,
                take_out_access_point_id TEXT NULL,
                put_in_text TEXT NULL,
                take_out_text TEXT NULL,
                start_time_local TEXT NOT NULL,
                finish_time_local TEXT NULL,
                duration_minutes INTEGER NULL,
                review_status TEXT NOT NULL DEFAULT 'unreviewed',
                pipeline_stage TEXT NOT NULL DEFAULT 'structured',
                source TEXT NOT NULL DEFAULT 'personal',
                source_reference TEXT NULL,
                confidence REAL NULL,
                notes TEXT NULL,
                created_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                CHECK (review_status IN ('unreviewed', 'provisional', 'accepted', 'rejected')),
                CHECK (pipeline_stage IN ('raw', 'structured', 'normalized', 'reviewed', 'promoted')),
                CHECK (confidence IS NULL OR (confidence >= 0 AND confidence <= 1)),
                FOREIGN KEY (run_id) REFERENCES runs (id),
                FOREIGN KEY (put_in_access_point_id) REFERENCES access_points (id),
                FOREIGN KEY (take_out_access_point_id) REFERENCES access_points (id)
            );

            CREATE TABLE IF NOT EXISTS gauges (
                id TEXT PRIMARY KEY NOT NULL,
                river_name TEXT NOT NULL,
                station_id TEXT NULL,
                name TEXT NOT NULL,
                source TEXT NOT NULL DEFAULT 'USGS',
                source_reference TEXT NULL,
                confidence REAL NULL,
                river_mile REAL NULL,
                river_mile_source TEXT NULL,
                river_mile_confidence REAL NULL,
                review_status TEXT NOT NULL DEFAULT 'unreviewed',
                notes TEXT NULL,
                created_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                updated_at_utc TEXT NULL,
                CHECK (review_status IN ('unreviewed', 'provisional', 'accepted', 'rejected')),
                CHECK (confidence IS NULL OR (confidence >= 0 AND confidence <= 1)),
                CHECK (river_mile_confidence IS NULL OR (river_mile_confidence >= 0 AND river_mile_confidence <= 1))
            );

            CREATE TABLE IF NOT EXISTS run_gauge_links (
                id TEXT PRIMARY KEY NOT NULL,
                run_id TEXT NOT NULL,
                gauge_id TEXT NOT NULL,
                relationship TEXT NOT NULL,
                source TEXT NOT NULL DEFAULT 'manual_estimate',
                source_reference TEXT NULL,
                confidence REAL NULL,
                notes TEXT NULL,
                review_status TEXT NOT NULL DEFAULT 'unreviewed',
                created_at_utc TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
                updated_at_utc TEXT NULL,
                CHECK (relationship IN ('between_run', 'upstream', 'downstream', 'tributary', 'regional_reference')),
                CHECK (review_status IN ('unreviewed', 'provisional', 'accepted', 'rejected')),
                CHECK (confidence IS NULL OR (confidence >= 0 AND confidence <= 1)),
                FOREIGN KEY (run_id) REFERENCES runs (id),
                FOREIGN KEY (gauge_id) REFERENCES gauges (id)
            );

            CREATE INDEX IF NOT EXISTS IX_runs_river_name
                ON runs (river_name);

            CREATE INDEX IF NOT EXISTS IX_access_points_river_name
                ON access_points (river_name);

            CREATE INDEX IF NOT EXISTS IX_access_point_identifiers_access_point_id
                ON access_point_identifiers (access_point_id);

            CREATE INDEX IF NOT EXISTS IX_completed_run_observations_run_id
                ON completed_run_observations (run_id);

            CREATE INDEX IF NOT EXISTS IX_gauges_river_name
                ON gauges (river_name);

            CREATE INDEX IF NOT EXISTS IX_run_gauge_links_run_id
                ON run_gauge_links (run_id);
            """;
        command.ExecuteNonQuery();
    }
}
