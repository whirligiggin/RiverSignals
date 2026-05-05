# RiverSignals Intent

## Product Identity

RiverSignals is a river run data management and trip-planning system.

It includes a rule-based extraction capability, but it is no longer only an
extraction service. The accepted operating model combines:

- structured extraction from messy trip text
- curated and seeded river run catalog data
- access-point and run stewardship
- trip-time estimation from known run context
- flow and observation ingestion
- completed-run reporting
- request intake for runs beyond the seed catalog
- internal review surfaces for unresolved source material

## Primary Intent

RiverSignals helps paddlers and stewards turn fragmented river-run information
into reviewable, durable, and useful planning records.

The system should make it easier to:

- estimate known access-to-access river runs
- collect requests for runs the system does not fully know yet
- collect completed-run observations without treating them as automatic truth
- preserve provenance and review state for source material
- support later stewardship decisions with visible evidence

## Non-Negotiable Product Truths

- Public estimates are planning aids, not guarantees.
- Stored user submissions are evidence, not truth.
- Raw source material must remain distinguishable from reviewed records.
- Data stewardship is explicit and reviewable.
- Runtime persistence matters because requests, observations, reviews, and flow
  readings are durable product state.
- Internal stewardship concepts must not silently leak into public planning
  surfaces.
- Extraction remains a component capability, not the whole product identity.

## Product Boundaries

In scope for the current system:

- known-run estimation
- run request intake
- completed-run observation intake
- flow reading storage and latest-flow lookup
- seeded catalog reads
- canonical/provisional data stewardship
- rule-based trip report extraction

Out of scope until explicitly missioned:

- authenticated accounts
- automated trust or promotion of submitted records
- route engine behavior
- hydrology or forecasting beyond bounded flow/outlook support
- automated backup jobs
- ad integration
- personalized dashboard behavior

## Application Classification

`SignalExtraction.Api` is the current RiverSignals application surface.

`SignalExtraction.Core` contains shared domain models, extraction logic,
estimation services, catalog services, persistence services, and stewardship
services used by the current application.

`ProjectCignal` is a legacy/prototype trip-estimation application. It
demonstrates the earlier focused estimation slice, but it is not the current
RiverSignals launch surface and should not receive new product expansion unless
a future mission explicitly reclassifies or retires it.

## Scope Posture

Current complexity is accepted only as documented product surface, not as a
blank check for further expansion.

Future missions should either:

- preserve a capability because it supports the product intent above
- isolate a capability because it belongs to a different boundary
- defer a capability because its governance or verification is not ready
- reduce a capability because it does not support the accepted intent
