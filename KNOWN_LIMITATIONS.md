# Known Limitations

This is a **demo/prototype** FHIR server. The following limitations are documented intentionally and would be addressed before production use.

## Security

- **No authentication or authorization** — Production would implement SMART on FHIR or JWT bearer auth. All endpoints are currently open.
- **No rate limiting** on bulk export kick-off — an attacker could trigger resource exhaustion.
- **SQL column names in search** — `Search.buildWhere` interpolates column names into SQL. All current callers use hardcoded values, but the public function surface accepts arbitrary strings. Production would validate against a whitelist.
- **Hardcoded credentials** in `appsettings.json` — Development convenience. Production would use environment variables or secret management.

## FHIR Compliance

- **`_since` parameter** not supported in bulk export — required for incremental exports in production.
- **Unknown search parameters silently ignored** — FHIR spec requires warning or error with `Prefer: handling=strict`.
- **CapabilityStatement** does not declare search parameters per resource type.
- **`Bundle.id`** missing from search responses.
- **Date search** uses exact timestamp match, not FHIR day-range semantics (e.g., `eq2020-01-15` should match the entire day).
- **Encounter date search** only checks `period_start`, ignoring `period_end` overlap.
- **`Prefer` header** not supported (`return=minimal`, `return=OperationOutcome`).
- **`_format` parameter** filtered but not honored.
- **`$davinci-data-export`** uses GET; FHIR async pattern recommends POST.
- **`general-practitioner`** search parameter not implemented (no column in schema).

## Performance

- **Organization/Practitioner bulk export** returns all records, not just those related to the exported group.
- **Import uses single-row INSERTs** — PostgreSQL `COPY` would be ~10x faster for large datasets.
- **No job eviction** — completed/expired export jobs and their temp NDJSON files are never cleaned up.
- **`resource_text` re-parsed** in search bundles to extract `id` — could return `id` alongside `resource_text` from SQL.

## Data

- **100 unique patients** (Synthea data is duplicated across 10 NDJSON batches; import deduplicates with `ON CONFLICT DO NOTHING`).
- **`ON CONFLICT DO NOTHING`** silently drops re-imported data with changes. Production would use `ON CONFLICT DO UPDATE`.
- **Import `importLine`** has ~280 lines of duplicated match arms — works correctly but should be table-driven for maintainability.
