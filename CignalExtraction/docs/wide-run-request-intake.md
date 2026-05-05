# Wide Run Request Intake

RiverSignals accepts public requests for runs that are not in the seeded catalog
through `/request-run` and `POST /api/run-requests`.

Requests require only a river, put-in text, and take-out text. Rough location,
notes, aliases, and source hints are preserved when provided.

Stored requests are durable evidence, not trusted truth:

- runs are stored as `verification_status = provisional`
- runs are stored as `review_status = unreviewed`
- raw put-in and take-out text is preserved in raw access point candidate rows
- aliases and source hints are preserved as identifier evidence when supplied
- duplicates are allowed
- no merge, dedupe, reconciliation, promotion, routing, hydrology, forecasting,
  or estimate generation occurs
- seeded catalog estimate behavior remains unchanged
