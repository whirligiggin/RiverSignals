# Flow Persistence Plan

## Purpose

This plan defines a minimal durable-storage path for flow readings while preserving
the current API behavior, service boundary, and trip estimation behavior.

This is a planning document only. It does not introduce SQLite packages, database
files, migrations, or runtime behavior changes.

## Recommended Storage Approach

Use SQLite as the v1 durable store for flow readings.

SQLite is the recommended first persistence step because it provides durable local
storage, simple deployment, and queryable latest-by-segment behavior without
introducing a server database. It is a better fit than a JSON file for ordered
lookups, indexing, concurrent request handling, and later growth.

## Preserved Boundaries

The current `IFlowReadingService` contract remains the application boundary:

```csharp
void AddFlowReading(FlowReading reading);
FlowReading? GetLatestForSegment(Guid segmentId);
```

Current API behavior remains unchanged:

- `POST /api/flow` ingests one flow reading.
- `GET /api/flow/{segmentId}` returns the latest flow reading for the segment.
- Missing readings return `404 Not Found` at the API boundary.

Current estimation behavior remains unchanged:

- Trip estimation asks `IFlowReadingService` for the latest reading when a segment
  id is present.
- A reading is usable for estimation only when it includes `EstimatedCurrentMph`.
- If no usable flow reading exists, estimation falls back to manual/default current
  behavior.

`InMemoryFlowReadingService` should remain available for tests and lightweight
scenarios.

## Proposed SQLite Implementation

Add a SQLite-backed implementation alongside the current in-memory implementation,
tentatively named `SqliteFlowReadingService`.

The SQLite implementation should implement `IFlowReadingService` directly. API and
estimation callers should continue depending only on `IFlowReadingService`.

## Proposed Data Shape

Table: `FlowReadings`

| Column | Type | Required | Notes |
| --- | --- | --- | --- |
| `Id` | text | yes | GUID primary key |
| `SegmentId` | text | yes | GUID; indexed |
| `ObservedAtUtc` | text | yes | UTC timestamp; indexed with `SegmentId` |
| `EstimatedCurrentMph` | real | no | Nullable current estimate used by trip estimation |
| `GaugeHeightFeet` | real | no | Nullable gauge height |
| `FlowRateCfs` | real | no | Nullable flow rate |
| `Source` | text | yes | Source name, such as `USGS` or `Manual` |
| `SourceReference` | text | no | Gauge id, station id, URL, or other source reference |
| `CreatedAtUtc` | text | yes | Persistence audit timestamp |

Recommended indexes:

- Primary key on `Id`
- Index on `SegmentId`
- Composite index on `SegmentId`, `ObservedAtUtc`

## Read and Write Flow

### Add a flow reading

1. API receives `POST /api/flow`.
2. API validates the request using current validation behavior.
3. API maps the request to `FlowReading`.
4. API calls `IFlowReadingService.AddFlowReading(reading)`.
5. SQLite implementation inserts one row into `FlowReadings`.
6. API returns the current success response shape.

The persistence implementation should not introduce source ranking, staleness
policy, duplicate detection, or external provider behavior.

### Retrieve latest reading by segment

1. API receives `GET /api/flow/{segmentId}` or estimation requests latest flow.
2. Caller invokes `IFlowReadingService.GetLatestForSegment(segmentId)`.
3. SQLite implementation filters by `SegmentId`.
4. It orders by `ObservedAtUtc` descending.
5. If timestamps tie, it orders by `CreatedAtUtc` descending.
6. If a tie remains, it orders by `Id` for deterministic behavior.
7. It returns the first row mapped to `FlowReading`, or null when no row exists.

## Latest-Reading Definition

The latest reading is the reading for a segment with the greatest `ObservedAtUtc`.

Tie-breakers:

1. Newer `CreatedAtUtc`
2. Deterministic `Id` ordering

This preserves the current in-memory behavior while making tie handling explicit
for durable storage.

## Migration Path

1. Add SQLite package references in the implementation mission.
2. Add `SqliteFlowReadingService` alongside `InMemoryFlowReadingService`.
3. Keep `InMemoryFlowReadingService` unchanged for unit tests and lightweight use.
4. Add configuration for API registration:
   - in-memory for tests/local lightweight mode
   - SQLite for durable mode
5. Register the selected implementation as `IFlowReadingService`.
6. Keep API request/response contracts unchanged.
7. Keep trip estimation logic unchanged.

No migration from existing in-memory data is required because current data does
not survive application restart.

## Test Strategy for Implementation Mission

The later persistence implementation should include tests for:

- Adding then retrieving a reading by segment.
- Multiple readings for the same segment returning the newest `ObservedAtUtc`.
- Readings for other segments being ignored.
- Missing segment returning null at the service level.
- Missing segment returning `404 Not Found` at the API level.
- Readings surviving service or app recreation.
- Existing API ingestion and visibility tests continuing to pass.
- Existing trip estimation tests continuing to pass.

Suggested verification commands:

```powershell
dotnet test CignalExtraction\SignalExtraction.Core.Tests\SignalExtraction.Core.Tests.csproj --filter FlowApiIngestionTests
dotnet test CignalExtraction\SignalExtraction.Core.Tests\SignalExtraction.Core.Tests.csproj
```

## Explicit Non-Goals

- No external flow provider integration, including USGS.
- No flow staleness policy.
- No source ranking.
- No authentication or rate limiting.
- No UI changes.
- No changes to estimation algorithms.
- No changes to the public flow API contract.

## Open Questions for Future Work

- Which SQLite package should the implementation use?
- Should the SQLite implementation live in `SignalExtraction.Core` or a separate
  infrastructure project if the solution grows?
- Should operational configuration use appsettings, environment variables, or both?
- Should the API expose historical flow readings later, or only latest readings?
