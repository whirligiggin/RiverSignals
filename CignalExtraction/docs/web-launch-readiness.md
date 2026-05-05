# RiverSignals Web Launch Readiness

This document records the launch configuration, smoke checks, runtime artifact
hygiene, and go/no-go posture for the public RiverSignals web surface.

## Deployment Configuration

RiverSignals is an ASP.NET application. The launch-critical configuration is
the durable SQLite database connection string.

Required production setting:

```text
ConnectionStrings__RiverSignals=Data Source=<durable-host-path>/riversignals.db
```

Local development default:

```text
CignalExtraction/data/riversignals.db
```

When the API runs from `CignalExtraction/SignalExtraction.Api`, the checked-in
default connection string is:

```json
{
  "ConnectionStrings": {
    "RiverSignals": "Data Source=../data/riversignals.db"
  }
}
```

Store-specific overrides remain supported for compatibility:

- `ConnectionStrings__FlowReadings`
- `ConnectionStrings__RawAccessReviews`

For launch, prefer one shared `RiverSignals` database unless the host requires
separate files.

## Persistence And Backup

The SQLite database file is the durable unit of record. The configured database
path must point to storage that survives process restarts and redeploys.

Backup expectation:

- back up the configured `riversignals.db` file regularly
- pause writes or use SQLite-safe backup tooling while copying the file
- keep at least one recent backup outside the application deployment directory

Restore expectation:

- stop the app
- replace the configured database file with the selected backup
- start the app
- run launch smoke checks

This mission does not add automated backup jobs.

## Public Route Smoke Checks

Run these checks against the deployed base URL:

```powershell
$baseUrl = "https://<host>"
Invoke-WebRequest "$baseUrl/" -UseBasicParsing
Invoke-WebRequest "$baseUrl/request-run" -UseBasicParsing
Invoke-WebRequest "$baseUrl/completed-run" -UseBasicParsing
Invoke-WebRequest "$baseUrl/api/segments" -UseBasicParsing
```

Expected result:

- `/` returns the public Estimate surface
- `/request-run` returns the public request flow
- `/completed-run` returns the completed-run submission flow
- `/api/segments` returns active seeded run data

## Internal Route Boundary Checks

Internal tools are intentionally under `/internal` or `/api/internal`.

Known internal routes:

- `/internal/data-steward`
- `/api/internal/data-steward/tables`
- `/internal/raw-access-candidates`

Expected launch boundary:

- public pages do not link to internal routes
- internal routes remain explicit by path
- internal routes are not represented as public navigation

This mission does not add authentication. Public exposure of internal routes is
a deployment policy risk to handle before a broad public launch.

## Runtime Artifact Hygiene

Runtime files must stay out of commits. `.gitignore` excludes known local
artifacts:

- `CignalExtraction/data/*.db`
- `CignalExtraction/data/*.db-*`
- `CignalExtraction/SignalExtraction.Api/*.db`
- `CignalExtraction/SignalExtraction.Api/*.db-*`
- `tmp-*.log`
- `tmp-*.err.log`

Current local runtime artifacts may exist after tests or manual app runs. They
are operational residue, not mission records.

## Launch Checklist

Before launch:

- configure `ConnectionStrings__RiverSignals` to durable storage
- verify the database directory exists or can be created by the app process
- confirm backup and restore responsibility
- run public route smoke checks
- run internal route boundary checks
- run the full known test project
- confirm runtime DB/log files are not staged for commit
- record final go/no-go recommendation

## Rollback Notes

If launch smoke checks fail:

- stop the app or remove it from public routing
- restore the previous deployable artifact
- restore the prior SQLite database backup only if data corruption occurred
- rerun smoke checks against the restored version

If only public copy or route presentation is wrong, prefer rolling back the app
artifact without replacing the database.

## Go / No-Go Recommendation

Current implementation posture: conditional go for a controlled beta once the
hosting environment provides durable SQLite storage and the smoke checks above
pass against that host.

Known launch caveat: internal routes are path-separated but not authenticated by
this mission.
