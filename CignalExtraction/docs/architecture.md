# RiverSignals Architecture

## Current System Shape

RiverSignals currently lives under `CignalExtraction` and contains two
application surfaces plus shared domain code:

- `SignalExtraction.Api`
- `SignalExtraction.Core`
- `SignalExtraction.Core.Tests`
- `ProjectCignal`

The current product boundary is RiverSignals, implemented primarily by
`SignalExtraction.Api` and `SignalExtraction.Core`.

`ProjectCignal` is a legacy/prototype application and is outside the current
launch architecture unless a future mission explicitly changes that status.

## Application Boundaries

### SignalExtraction.Api

`SignalExtraction.Api` is the current web/API host for RiverSignals.

It owns:

- public Segment Estimate page at `/`
- public Compare Runs page at `/compare-runs`
- public Request Any Run page at `/request-run`
- public Report Completed Run page at `/completed-run`
- public API endpoints for extraction, segments, flow, requests, reports, gauges,
  and related planning data
- internal stewardship routes under `/internal` and `/api/internal`
- runtime configuration and SQLite store wiring

It must not own domain truth directly when a service boundary exists in Core.

### SignalExtraction.Core

`SignalExtraction.Core` owns domain models and service behavior.

Current service families include:

- extraction services
- trip estimation services
- segment catalog services
- flow reading services
- completed-run observation services
- wide-run request services
- raw access candidate services
- provisional access choice/pair services
- canonical workbench and SQLite schema services
- USGS flow import and parsing services

Core services should remain independently testable and should not depend on UI
presentation.

### SignalExtraction.Core.Tests

`SignalExtraction.Core.Tests` is the current verification project for Core and
API behavior. It includes unit, service, storage, API, and HTML surface tests.

### ProjectCignal

`ProjectCignal` is a separate Razor Pages prototype for simple trip estimation.

Current classification:

- preserved as historical/prototype reference
- not the current public RiverSignals app
- not the owner of current product intent
- not a target for new features without an explicit mission

Future missions may choose to archive, isolate, document, migrate, or delete it,
but this mission does not change its code or behavior.

## Data And Authority Boundaries

Seeded catalog data currently remains authoritative for public known-run
estimation unless a future migration mission changes authority.

Runtime SQLite storage is durable product state for:

- flow readings
- completed-run observations
- wide-run requests
- raw access review metadata
- canonical/provisional stewardship records

Submitted requests, completed-run observations, raw access candidates, and
review notes do not automatically become trusted run catalog truth.

Canonical/provisional storage preserves structure, provenance, review/status,
and future stewardship state. It does not automatically infer, merge, promote,
deduplicate, or reconcile records.

## Public And Internal Boundaries

Public surfaces are for paddler-facing planning and submission workflows.

Current public workflow pages:

- `/`
- `/compare-runs`
- `/request-run`
- `/completed-run`

Internal surfaces are for stewardship and review:

- `/internal/data-steward`
- `/api/internal/data-steward/...`
- `/internal/raw-access-candidates`

Internal routes are path-separated but not authentication-protected in the
current implementation. Broad public launch requires a deployment policy decision
or a future authentication/access-control mission.

## Capability Classification

Preserve:

- rule-based extraction as a component capability
- known-run estimation
- seeded catalog reads
- durable request, observation, flow, and review storage
- internal stewardship boundaries
- public/internal route separation

Defer:

- authenticated accounts
- automated trust/promotion pipelines
- ranking or source freshness policy
- broad forecasting or hydrology
- ad and personalized dashboard behavior

Isolate:

- `ProjectCignal` as legacy/prototype surface
- internal stewardship routes from public navigation
- raw source material from reviewed catalog truth

Candidate for reduction:

- duplicate product descriptions that imply separate product identities
- service growth that lacks explicit ownership or verification
- prototype code paths if they are no longer useful as reference

## Architecture Rule For Future Work

Before adding a feature, future missions must identify:

- which product intent the feature supports
- which application owns the route or presentation
- which Core service owns domain behavior
- which store owns durable state
- which tests prove the behavior without redefining product truth
