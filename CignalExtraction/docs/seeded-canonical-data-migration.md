# Seeded Canonical Data Migration

RiverSignals seeds current catalog records into canonical SQLite tables at API
startup after schema creation.

The seeding path inserts missing rows only. Existing canonical records are not
overwritten, so human steward edits remain durable and are not silently
corrected by startup.

Seeded canonical rows preserve the current operating boundary:

- seeded catalog reads remain authoritative for public estimates
- canonical rows are durable records, not estimator authority
- `distance_miles` stays manually curated on seeded runs
- river-mile values remain provenance-bearing reference data
- gauge/run links remain explicit, provisional, and confidence-bearing
- no merge, dedupe, reconciliation, lookup, or promotion is performed

Verification is covered by `SqliteSeededCanonicalCatalogSeederTests`, including
row counts, provenance/status/confidence fields, explicit run-gauge links,
idempotence/no-overwrite behavior, and seeded estimate output non-regression.
