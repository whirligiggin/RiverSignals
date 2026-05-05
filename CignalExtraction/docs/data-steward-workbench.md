# Data Steward Workbench

`/internal/data-steward` is an internal-only workbench for inspecting and
directly editing durable canonical/provisional RiverSignals records.

The workbench covers:

- `runs`
- `access_points`
- `raw_access_point_candidates`
- `access_point_identifiers`
- `completed_run_observations`
- `gauges`
- `run_gauge_links`

Edits are explicit field updates against existing records. The workbench does
not merge, deduplicate, reconcile, promote, infer, normalize, or create trust.

Review/status fields govern later human interpretation only. Stored, edited, or
reviewed records do not automatically affect seeded catalog authority or trip
estimate behavior.
