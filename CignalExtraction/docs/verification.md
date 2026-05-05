# RiverSignals Verification

## Verification Purpose

Verification must prove that RiverSignals behavior matches product intent and
architecture boundaries.

Passing tests are not enough if they validate the wrong product identity. Tests
must preserve the distinction between:

- extraction component behavior
- public planning workflows
- internal stewardship workflows
- durable runtime state
- untrusted evidence and reviewed truth

## Verification Layers

### Component Verification

Use focused tests for Core services:

- extraction parsing and confidence behavior
- trip estimation calculations
- best-available estimate selection
- flow lookup and ingestion behavior
- request and observation storage
- candidate and canonical stewardship services

These tests should verify domain behavior without depending on HTML structure.

### API Verification

Use API tests for route contracts and persistence boundaries:

- request payload acceptance and rejection
- response shape stability
- SQLite persistence across app factory restarts where durable state matters
- no unintended API contract change
- no automatic trust, promotion, or authority transfer

### Public UI Verification

Use HTML surface tests for public workflow boundaries:

- `/` renders Segment Estimate as home
- `/compare-runs` renders Compare Runs
- `/request-run` renders Request Any Run
- `/completed-run` renders Report Completed Run
- public navigation does not expose internal stewardship routes
- public copy avoids internal confidence/schema/reconciliation terminology unless
  explicitly missioned

UI tests should guard structure and workflow ownership, not pixel-perfect design.

### Internal UI Verification

Use internal surface tests for stewardship boundaries:

- internal routes remain explicit under `/internal` or `/api/internal`
- review labels and notes do not automatically create trusted catalog truth
- data steward edits are explicit field updates
- internal tools do not imply public authority

### Persistence Verification

Persistence tests should verify:

- configured SQLite paths are honored
- durable stores survive relevant app/service restarts
- runtime database artifacts are not committed
- backup/restore expectations remain documented

## Required Mission Verification

For documentation-only intent/architecture missions:

- verify that product identity, architecture, and verification documents exist
- verify README points to the accepted product identity
- verify no code behavior was changed
- run tests only if documentation validation tests exist or if touched files can
  affect build behavior

For implementation missions:

- run the focused mission slice
- run the full known test project unless the Coordinator explicitly scopes a
  smaller verification boundary
- report unrelated failures separately

Current full known test command:

```powershell
dotnet test CignalExtraction/SignalExtraction.Core.Tests/SignalExtraction.Core.Tests.csproj
```

## Non-Negotiable Verification Rules

- Do not modify tests to bless an incorrect implementation.
- Do not use UI tests to redefine product intent.
- Do not treat submitted evidence as reviewed truth unless a mission explicitly
  changes authority.
- Do not hide internal route exposure by weakening public boundary tests.
- Do not describe the whole repo as healthy when only mission-specific tests ran.

## Follow-Up Mission Candidates

This verification posture enables later missions such as:

- add documentation validation tests for product intent and architecture docs
- isolate or retire `ProjectCignal`
- split oversized HTML builders into explicit presentation modules
- formalize authentication/access control for internal routes
- define source ranking or freshness policy for flow and observation data
