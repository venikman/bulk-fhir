# OpenTelemetry + Phoenix Integration

**Date:** 2026-03-18
**Status:** Approved

## Goal

Add OpenTelemetry observability (metrics, traces, logs) to the bulk-fhir API, exporting to Arize Phoenix — locally for dev, deployed instance on Fly.io for production.

## Architecture

```
Local dev:
  bulk-fhir (dotnet run)  ── OTLP gRPC ──►  Phoenix (docker-compose, localhost:4317)
                                              UI at localhost:6006

Production:
  bulk-fhir (Fly.io)  ── OTLP gRPC ──►  fhir-copilot-phoenix.internal:4317
                                          UI at fhir-copilot-phoenix.fly.dev
```

## Telemetry Signals

All built-in, no custom metrics in this iteration.

### Metrics
- ASP.NET Core HTTP server metrics (request duration, count, status codes by endpoint)
- HTTP client metrics (outbound calls if any)
- .NET runtime metrics (GC, thread pool, assembly count)
- Npgsql metrics (connection pool, command duration)

### Traces
- ASP.NET Core request spans (one per HTTP request, includes route, status)
- Npgsql command spans (SQL queries as child spans)
- HTTP client spans (outbound calls)

### Logs
- `ILogger` output piped through OTEL log exporter
- Logs correlated with traces via TraceId/SpanId

## Configuration

Standard OTEL environment variables — no custom env vars needed:

| Variable | Purpose | Local dev | Fly.io |
|----------|---------|-----------|--------|
| `OTEL_SERVICE_NAME` | Service identity | `bulk-fhir` | `bulk-fhir` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Phoenix OTLP | `http://localhost:4317` | `http://fhir-copilot-phoenix.internal:4317` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Wire protocol | `grpc` | `grpc` |

Single exporter, single endpoint. When `OTEL_EXPORTER_OTLP_ENDPOINT` is unset, OTEL is configured but exports nowhere (safe no-op).

## NuGet Packages

Added to `src/BulkFhir.Api/BulkFhir.Api.fsproj`:

| Package | Purpose |
|---------|---------|
| `OpenTelemetry.Extensions.Hosting` | Wires OTEL into ASP.NET DI |
| `OpenTelemetry.Instrumentation.AspNetCore` | HTTP server metrics + traces |
| `OpenTelemetry.Instrumentation.Http` | HTTP client metrics + traces |
| `OpenTelemetry.Instrumentation.Runtime` | GC, thread pool metrics |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP exporter |
| `Npgsql.OpenTelemetry` | Npgsql traces (metrics require manual `AddMeter("Npgsql")`) |

## File Changes

### `src/BulkFhir.Api/BulkFhir.Api.fsproj`
Add the 6 NuGet packages listed above.

### `src/BulkFhir.Api/Program.fs`
Add OTEL configuration between `CreateBuilder` and `Build`:

1. Configure OpenTelemetry metrics via `WithMetrics`: add ASP.NET Core, HTTP client, runtime meters, and `AddMeter("Npgsql")` (Npgsql emits metrics natively via `System.Diagnostics.Metrics`; the `Npgsql.OpenTelemetry` package does not auto-register the meter).
2. Configure OpenTelemetry tracing via `WithTracing`: add ASP.NET Core, HTTP client, and Npgsql activity sources.
3. Configure OpenTelemetry logging via `WithLogging`: add OTLP log exporter.
4. Use `UseOtlpExporter()` — single exporter, reads `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL` automatically from environment.

No changes to any handler, storage, or domain code.

### `docker-compose.yml` (new)
Phoenix for local development:

```yaml
services:
  phoenix:
    image: arizephoenix/phoenix:latest
    ports:
      - "6006:6006"    # Web UI
      - "4317:4317"    # OTLP gRPC receiver
```

### Remove dashboard API (dead code)

Delete these files:
- `src/BulkFhir.Api/DashboardHandlers.fs`
- `src/BulkFhir.Storage/Dashboard.fs`
- `tests/BulkFhir.Tests.E2E/DashboardTests.fs`
- `tests/BulkFhir.Tests.E2E/DashboardSchemaTests.fs`

Update these files:
- `src/BulkFhir.Api/BulkFhir.Api.fsproj` — remove `DashboardHandlers.fs` compile entry
- `src/BulkFhir.Storage/BulkFhir.Storage.fsproj` — remove `Dashboard.fs` compile entry
- `src/BulkFhir.Api/Program.fs` — remove 6 dashboard route lines
- `tests/.../BulkFhir.Tests.E2E.fsproj` — remove `DashboardTests.fs` and `DashboardSchemaTests.fs` compile entries, remove `Npgsql` package ref
- `tests/.../Program.fs` — remove `DashboardTests` and `DashboardSchemaTests` references
- `src/BulkFhir.Storage/Schema.fs` — remove 6 dashboard SQL views, `bulk_export_jobs` table
- `src/BulkFhir.Storage/Repository.fs` — remove `upsertBulkExportJob` function
- `src/BulkFhir.Api/BulkExport.fs` — remove `persistJob` calls and DB persistence (jobs stay in-memory only)

## What This Does NOT Change

- No handler code changes — instrumentation is automatic
- No storage layer changes — Npgsql tracing hooks in at the connection level
- No domain changes
- No custom metrics (future iteration)
- No changes to existing logging calls — they continue to work, now also exported via OTEL

## Verification

1. `dotnet build BulkFhir.slnx` — solution compiles
2. `docker compose up -d` + `dotnet run` locally with `OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317` — Phoenix at localhost:6006 shows traces/metrics/logs
3. `fly deploy -a bulk-fhir` (with env vars set) — traces appear in Phoenix at fhir-copilot-phoenix.fly.dev
