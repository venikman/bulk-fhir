# OpenTelemetry + Aspire Dashboard Integration

**Date:** 2026-03-18
**Status:** Approved

## Goal

Add OpenTelemetry observability (metrics, traces, logs) to the bulk-fhir API, exporting to both a .NET Aspire Dashboard and an existing Arize Phoenix collector on Fly.io.

## Architecture

```
bulk-fhir (Fly.io)
  ├─ OTLP ──► bulk-fhir-dashboard.internal:18889  (Aspire Dashboard)
  └─ OTLP ──► fhir-copilot-phoenix.internal:4317   (Phoenix collector)
```

All three are in the same Fly.io org and region (`iad`), communicating over private networking.

For local development, only the Aspire Dashboard runs (via docker-compose), and Phoenix is skipped.

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

Environment variables (OTEL standard + one custom):

| Variable | Purpose | Local dev | Fly.io |
|----------|---------|-----------|--------|
| `OTEL_SERVICE_NAME` | Service identity | `bulk-fhir` | `bulk-fhir` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Aspire Dashboard OTLP | `http://localhost:18889` | `http://bulk-fhir-dashboard.internal:18889` |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Wire protocol | `grpc` | `grpc` |
| `PHOENIX_OTLP_ENDPOINT` | Phoenix OTLP (optional) | not set | `http://fhir-copilot-phoenix.internal:4317` |

When `PHOENIX_OTLP_ENDPOINT` is unset, only the primary (Aspire) exporter is registered.

**Protocol note:** The Aspire Dashboard accepts gRPC on port 18889 and HTTP/protobuf on 18890. We use gRPC (port 18889). Setting `OTEL_EXPORTER_OTLP_PROTOCOL=grpc` explicitly prevents ambiguity across OTEL SDK versions.

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
4. For each signal, use the signal-specific `AddOtlpExporter("aspire", fun opts -> ...)` overload, reading `OTEL_EXPORTER_OTLP_ENDPOINT` via `Environment.GetEnvironmentVariable`. Do NOT use the cross-cutting `UseOtlpExporter()` — it can only be called once and does not support dual export.
5. If `PHOENIX_OTLP_ENDPOINT` is set, add a second `AddOtlpExporter("phoenix", fun opts -> ...)` on each signal builder, reading the Phoenix endpoint from the env var.

No changes to any handler, storage, or domain code.

### `docker-compose.yml` (new)
Aspire Dashboard for local development:

```yaml
services:
  dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:9.4
    ports:
      - "18888:18888"   # Web UI
      - "18889:18889"   # OTLP gRPC receiver
    environment:
      DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS: "true"
```

### `infra/aspire-dashboard/fly.toml` (new)
Fly.io deployment for the Aspire Dashboard:

- App name: `bulk-fhir-dashboard`
- Image: `mcr.microsoft.com/dotnet/aspire-dashboard:9.4`
- Region: `iad`
- Web UI: exposed on HTTPS via `[http_service]` (internal port 18888)
- OTLP: port 18889 must NOT appear in any `[[services]]` or `[http_service]` block — it is reachable over Fly's 6PN private network automatically because the container binds to `0.0.0.0:18889`
- Environment: `DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true`
- VM: shared-cpu-1x, 512MB (dashboard is lightweight)

## What This Does NOT Change

- No handler code changes — instrumentation is automatic
- No storage layer changes — Npgsql tracing hooks in at the connection level
- No domain changes
- No custom metrics (future iteration)
- No changes to existing logging calls — they continue to work, now also exported via OTEL

## Verification

1. `dotnet build BulkFhir.slnx` — solution compiles
2. `docker compose up -d` + `dotnet run` locally — dashboard at localhost:18888 shows traces/metrics/logs
3. `fly deploy -a bulk-fhir-dashboard` — dashboard accessible on Fly.io
4. `fly deploy -a bulk-fhir` (with env vars set) — traces appear in both Aspire and Phoenix
