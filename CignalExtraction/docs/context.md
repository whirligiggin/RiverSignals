# RiverSignals Extraction Component Context

This document describes the extraction component of RiverSignals.

RiverSignals as a product is broader than extraction. Product-level intent,
architecture, and verification boundaries are defined in:

- [`intent.md`](intent.md)
- [`architecture.md`](architecture.md)
- [`verification.md`](verification.md)

The extraction component remains focused on extracting structured river trip
information from messy, free-form text.

## Current extraction responsibilities

- Put-in and pull-out locations
- Watercraft type
- Duration normalized to hours
- Trip timing phrase
- Conditions or notes
- Classification of record type and duration type
- Confidence scoring

## Guiding principles

### Location extraction
- Prefer strong anchor phrases first: `started at`, `put in at`, `launched at`, `began at`, `took out at`, `pull out at`, `ended at`, `finished at`
- Only fall back to `X to Y` route parsing when anchors are not found
- Reject candidates that are too long, include motion verbs, or look like sentence fragments
- Keep extracted locations short and place-like, trusting capitalized noun phrases when available
- Do not infer locations from generic verb + preposition patterns unless the verb is a trusted anchor

### Time extraction
- Prefer explicit dates first
- Use a written post date to infer relative event dates, especially weekday references like `Saturday`
- Capture strong time-of-day anchors such as `sundown`, `sunrise`, `dawn`, `dusk`
- Preserve exact matched time phrases and include nearby context for review
- Record relative timing estimates in notes when precise timing must be inferred

### Watercraft extraction
- Detect explicit known watercraft: kayak, canoe, raft, cataraft, drift boat, inflatable kayak, whitewater kayak
- Infer kayak from length/style descriptors such as `12-13 foot recreation/fishing kayaks`
- Avoid false positives from non-watercraft phrases like `canoe camp`

### Notes and estimation
- Capture overnight camping and state park references in `ConditionsOrNotes`
- Track gauge mentions as a first step toward flow-based timing estimation
- Use extracted dates and relative weekday cues to build simple temporal estimates

## Current implementation notes

- `ExtractionService` is rule-based and intentionally simple
- `ExtractionConfidence` is scaled to start at 15% and cap below 100%
- The current logic intentionally maintains reviewability over aggressive inference
