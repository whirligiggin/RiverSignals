# RiverSignals


> Note: This README describes the CignalExtraction/RiverSignals product area.
> For repository governance and mission startup, see `/Arbor/...`.

RiverSignals is a .NET river run data management and trip-planning system. It
includes a rule-based extraction component, but the current product is broader
than extraction: it manages known run context, trip-time estimates, run
requests, completed-run observations, flow readings, and internal stewardship
records.

Product-level boundaries are documented in:

- [`docs/intent.md`](docs/intent.md)
- [`docs/architecture.md`](docs/architecture.md)
- [`docs/verification.md`](docs/verification.md)

## What it does

- Estimates known access-to-access river runs
- Compares known runs using existing estimate behavior
- Accepts requests for runs beyond the seeded catalog
- Accepts completed-run observations as evidence, not automatic truth
- Stores flow readings and uses latest usable current where supported
- Provides internal stewardship surfaces for canonical/provisional data
- Extracts structured trip details from noisy text as a component capability

 - [estimate screenshot](CignalExtraction/docs/screenshots/Estimate-A-Run.png)
 
## Project structure

- `SignalExtraction.Api/` - current RiverSignals web/API host
- `SignalExtraction.Core/` - domain models, extraction, estimation, catalog, persistence, and stewardship services
- `SignalExtraction.Core.Tests/` - Core/API/UI verification project
- `ProjectCignal/` - legacy/prototype trip-estimation app, not the current launch surface
- `docs/` - product intent, architecture, verification, operations, and component context

## Run locally

1. Restore and build:
   ```powershell
   dotnet build
   ```
2. Run the API:
   ```powershell
   cd SignalExtraction.Api
   dotnet run
   ```
3. Open the local URL shown by the API host, or call endpoints such as
   `/extract`, `/api/segments`, `/api/flow/{segmentId}`, and `/api/run-requests`.

## Product boundaries

`SignalExtraction.Api` is the current RiverSignals application surface.

`ProjectCignal` is preserved as a legacy/prototype trip-estimation application.
It demonstrates the earlier focused estimator slice, but it is not the current
RiverSignals launch surface and should not receive new product expansion unless
a future mission explicitly reclassifies it.

Extraction remains a component capability. Its current behavior is documented in
[`docs/context.md`](docs/context.md).

## Persistence

Runtime data is durable in SQLite. The default local database is:

```text
CignalExtraction/data/riversignals.db
```

The API uses `ConnectionStrings:RiverSignals` by default, with existing named
connection strings available for store-specific overrides. See
[`docs/persistence.md`](docs/persistence.md) for local, deployable, and backup
expectations.

For public launch configuration, smoke checks, runtime artifact hygiene, and
rollback notes, see
[`docs/web-launch-readiness.md`](docs/web-launch-readiness.md).

## Flow API

The API also exposes a minimal SQLite-backed flow-reading contract. Flow readings
are stored through the existing Core flow service and persist as long as the
configured database file is preserved.

### Ingest a flow reading

`POST /api/flow`

Example request:

```json
{
  "segmentId": "11111111-1111-1111-1111-111111111111",
  "observedAtUtc": "2026-04-22T15:00:00Z",
  "flowRateCfs": 2400,
  "estimatedCurrentMph": 2.4,
  "gaugeHeightFeet": 4.1,
  "source": "USGS",
  "sourceReference": "Gauge_12345"
}
```

Required input:

- `segmentId`
- `observedAtUtc`
- `source`
- at least one flow measurement: `flowRateCfs`, `estimatedCurrentMph`, or `gaugeHeightFeet`

Example success response:

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "segmentId": "11111111-1111-1111-1111-111111111111",
  "observedAtUtc": "2026-04-22T15:00:00Z",
  "estimatedCurrentMph": 2.4,
  "flowRateCfs": 2400,
  "source": "USGS"
}
```

Invalid payloads return `400 Bad Request` and are not stored.

### Read the latest flow reading

`GET /api/flow/{segmentId}`

Example success response:

```json
{
  "id": "22222222-2222-2222-2222-222222222222",
  "segmentId": "11111111-1111-1111-1111-111111111111",
  "observedAtUtc": "2026-04-22T15:00:00Z",
  "estimatedCurrentMph": 2.4,
  "gaugeHeightFeet": 4.1,
  "flowRateCfs": 2400,
  "source": "USGS",
  "sourceReference": "Gauge_12345"
}
```

If no reading exists for the segment, the API returns `404 Not Found`.

### Current flow behavior

- Flow storage uses SQLite by default.
- Flow readings persist across application restarts when the configured SQLite
  database file is preserved.
- Latest-flow selection uses the existing `IFlowReadingService` boundary.
- When trip estimation receives a `SegmentId`, it uses the latest flow reading for
  that segment if the reading includes `estimatedCurrentMph`.
- If no usable flow reading exists, trip estimation falls back to manual/default
  current input.
- There is no authentication, source ranking, or staleness policy yet.

### Verification

Run focused flow API tests:

```powershell
dotnet test CignalExtraction\SignalExtraction.Core.Tests\SignalExtraction.Core.Tests.csproj --filter FlowApiIngestionTests
```

Run the known core/API test project:

```powershell
dotnet test CignalExtraction\SignalExtraction.Core.Tests\SignalExtraction.Core.Tests.csproj
```

## Notes

- The extraction logic is intentionally rule-based and optimized for reviewable matches.
- Confidence scoring is designed to surface higher-certainty extractions while preserving reviewability for ambiguous text.