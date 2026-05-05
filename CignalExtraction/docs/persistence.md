# RiverSignals Persistence Baseline

RiverSignals treats runtime data as durable.

## SQLite Stores

The current application uses SQLite for:

- flow readings
- completed-run observations
- raw access candidate review metadata

By default, all three stores use the shared `RiverSignals` database connection
string:

```json
{
  "ConnectionStrings": {
    "RiverSignals": "Data Source=../data/riversignals.db"
  }
}
```

The existing named connection strings are still supported:

- `FlowReadings`
- `RawAccessReviews`

When configured, the named strings take precedence for their respective stores.
If they are omitted, the stores fall back to `RiverSignals`.

## Local Database Location

For local development, the default database is:

```text
CignalExtraction/data/riversignals.db
```

When the API is run from `CignalExtraction/SignalExtraction.Api`, the relative
path `../data/riversignals.db` resolves to that file.

SQLite parent directories are created automatically before a store opens its
database.

## Deployable Configuration

Production or hosted environments should set explicit connection strings rather
than relying on the local relative default.

Recommended shape:

```text
ConnectionStrings__RiverSignals=Data Source=/var/lib/riversignals/riversignals.db
```

Host-specific paths are acceptable as long as they point to durable storage that
survives process restarts and redeploys.

## Backup And Restore

For a SQLite deployment, the database file is the durable unit of record.

Operational expectation:

- back up the configured database file regularly
- copy the database only when writes are paused or by using SQLite-safe backup
  tooling
- restore by replacing the configured database file before application startup

This mission does not add automated backups.

## Runtime Artifact Hygiene

Runtime database files and logs are not mission artifacts and should not be
committed.

Known local runtime artifacts include:

- `CignalExtraction/SignalExtraction.Api/flow-readings.db`
- `tmp-*.log`
- `tmp-*.err.log`

Future missions may add `.gitignore` entries or deployment-specific cleanup if
explicitly scoped.

## Store Boundaries

The canonical schema baseline creates durable tables for future data stewardship:

- `runs`
- `access_points`
- `raw_access_point_candidates`
- `access_point_identifiers`
- `completed_run_observations`
- `gauges`
- `run_gauge_links`

These tables are schema-only in the current operating model. Existing seeded
catalog reads, public estimate behavior, and completed-run submission behavior
remain on their established paths until a later migration mission explicitly
moves authority into canonical storage.

Canonical rows preserve provenance, review/status, confidence, and optional
river-mile reference fields where structurally relevant. Schema creation does
not infer, dedupe, merge, reconcile, promote, or trust records automatically.
