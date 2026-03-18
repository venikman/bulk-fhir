# OTEL + Phoenix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add OpenTelemetry observability (metrics, traces, logs) to bulk-fhir, exporting to Arize Phoenix locally and on Fly.io. Remove unused dashboard API code.

**Architecture:** Wire OTEL SDK into the ASP.NET pipeline via `Program.fs`. Single OTLP exporter pointing at Phoenix. Local dev uses docker-compose for Phoenix. Dashboard API endpoints and backing code are dead code and get removed.

**Tech Stack:** OpenTelemetry .NET SDK, Arize Phoenix, Falco/ASP.NET Core, Npgsql, Fly.io

**Spec:** `docs/superpowers/specs/2026-03-18-otel-aspire-design.md`

---

### Task 1: Remove dashboard dead code — delete files

**Files:**
- Delete: `src/BulkFhir.Api/DashboardHandlers.fs`
- Delete: `src/BulkFhir.Storage/Dashboard.fs`
- Delete: `tests/BulkFhir.Tests.E2E/DashboardTests.fs`
- Delete: `tests/BulkFhir.Tests.E2E/DashboardSchemaTests.fs`

- [ ] **Step 1: Delete the 4 dashboard files**

```bash
rm src/BulkFhir.Api/DashboardHandlers.fs
rm src/BulkFhir.Storage/Dashboard.fs
rm tests/BulkFhir.Tests.E2E/DashboardTests.fs
rm tests/BulkFhir.Tests.E2E/DashboardSchemaTests.fs
```

- [ ] **Step 2: Remove compile entries from project files**

In `src/BulkFhir.Api/BulkFhir.Api.fsproj`, remove this line:
```xml
    <Compile Include="DashboardHandlers.fs" />
```

In `src/BulkFhir.Storage/BulkFhir.Storage.fsproj`, remove this line:
```xml
    <Compile Include="Dashboard.fs" />
```

In `tests/BulkFhir.Tests.E2E/BulkFhir.Tests.E2E.fsproj`, remove these lines:
```xml
    <Compile Include="DashboardTests.fs" />
    <Compile Include="DashboardSchemaTests.fs" />
```

And remove this package reference (only used by DashboardSchemaTests):
```xml
    <PackageReference Include="Npgsql" Version="10.0.2" />
```

- [ ] **Step 3: Remove dashboard routes from Program.fs**

In `src/BulkFhir.Api/Program.fs`, remove lines 15-21 (the dashboard comment and 6 route registrations):
```fsharp
        // Dashboard
        get "/dashboard-api/v1/overview" DashboardHandlers.overview
        get "/dashboard-api/v1/resources" DashboardHandlers.resources
        get "/dashboard-api/v1/groups" DashboardHandlers.groups
        get "/dashboard-api/v1/exports" DashboardHandlers.exports
        get "/dashboard-api/v1/quality" DashboardHandlers.quality
        get "/dashboard-api/v1/resource/{resourceType}/{id}" DashboardHandlers.rawResource
```

- [ ] **Step 4: Remove dashboard references from Fixtures.fs**

In `tests/BulkFhir.Tests.E2E/Fixtures.fs`, remove these 6 dashboard route lines (lines 26-31):
```fsharp
            get "/dashboard-api/v1/overview" DashboardHandlers.overview
            get "/dashboard-api/v1/resources" DashboardHandlers.resources
            get "/dashboard-api/v1/groups" DashboardHandlers.groups
            get "/dashboard-api/v1/exports" DashboardHandlers.exports
            get "/dashboard-api/v1/quality" DashboardHandlers.quality
            get "/dashboard-api/v1/resource/{resourceType}/{id}" DashboardHandlers.rawResource
```

Also remove the `"Dashboard:InternalApiKey"` config entry (line 48):
```fsharp
                        "Dashboard:InternalApiKey", "test-dashboard-key"
```

- [ ] **Step 5: Remove dashboard test references from test Program.fs**

In `tests/BulkFhir.Tests.E2E/Program.fs`, remove these two lines:
```fsharp
                BulkFhir.Tests.DashboardTests.tests fixture
                BulkFhir.Tests.DashboardSchemaTests.tests
```

- [ ] **Step 6: Build to verify**

Run: `dotnet build BulkFhir.slnx`
Expected: Build succeeded. 0 Error(s)

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Remove unused dashboard API (DashboardHandlers, Dashboard.fs, views, tests)"
```

---

### Task 2: Remove dashboard DB schema and job persistence

**Files:**
- Modify: `src/BulkFhir.Storage/Schema.fs` (lines 221-728)
- Modify: `src/BulkFhir.Storage/Repository.fs` (lines 99-146)
- Modify: `src/BulkFhir.Api/BulkExport.fs`

- [ ] **Step 1: Remove bulk_export_jobs table and dashboard views from Schema.fs**

In `src/BulkFhir.Storage/Schema.fs`, remove everything from line 221 (`-- Bulk export jobs surfaced in the dashboard`) through line 728 (end of `dashboard_overview_v` view), inclusive. The `"""` closing the DDL string and the `createSchema` function below must remain.

After removal, the DDL should end with the `imaging_studies` table and index (lines 211-219), then the closing `"""`.

- [ ] **Step 2: Remove upsertBulkExportJob from Repository.fs**

In `src/BulkFhir.Storage/Repository.fs`, remove the entire `upsertBulkExportJob` function (lines 99-146), including the doc comment on line 99.

- [ ] **Step 3: Remove persistence from BulkExport.fs**

In `src/BulkFhir.Api/BulkExport.fs`:

Remove these private helper functions entirely:
```fsharp
    let private statusToText status = ...
    let private progressText status = ...
    let private typesToText (types: FhirResourceType list) = ...
    let private persistJob (connString: string) (job: ExportJob) = ...
```

Simplify `createJob` — remove `connString` parameter and `persistJob` call:
```fsharp
    let createJob (groupId: string) (requestUrl: string) (types: FhirResourceType list) =
        let jobId = Guid.NewGuid().ToString("N").[..7]
        let jobDir = Path.Combine(exportDir, jobId)
        Directory.CreateDirectory(jobDir) |> ignore
        let job =
            { Id = jobId; GroupId = groupId; RequestUrl = requestUrl
              Types = types; Status = Pending; CreatedAt = DateTime.UtcNow
              CompletedAt = None; ExpiresAt = None; OutputDir = jobDir }
        jobs.[jobId] <- job
        job
```

Simplify `updateJob` — remove `connString` parameter and `persistJob` call:
```fsharp
    let updateJob (job: ExportJob) =
        jobs.[job.Id] <- job
```

Simplify `expireJob` — remove `connString` parameter and `persistJob` call:
```fsharp
    let expireJob (jobId: string) =
        match jobs.TryGetValue(jobId) with
        | true, job ->
            let expiredJob = { job with Status = Expired; ExpiresAt = Some DateTime.UtcNow }
            jobs.[job.Id] <- expiredJob
            try
                if Directory.Exists(job.OutputDir) then
                    Directory.Delete(job.OutputDir, true)
            with _ -> ()
            true
        | _ -> false
```

Update `runExport` — calls to `updateJob` no longer pass `connString`:
```fsharp
    // Change all occurrences of:
    //   do! updateJob connString { job with ... }
    // To:
    //   updateJob { job with ... }
```

Remove `open BulkFhir.Storage` from BulkExport.fs if Repository is no longer referenced. Check: `Repository.streamAllToFile`, `Repository.readByIds`, `Repository.getBySubjectRefs` are still used in `runExport`, so keep the open.

- [ ] **Step 4: Update Handlers.fs callers**

In `src/BulkFhir.Api/Handlers.fs`:

`bulkExportKickoff` handler — update the `createJob` call (remove `connString` as first arg) and make it synchronous (no longer returns Task):
```fsharp
    // Change:
    //   let! job = BulkExport.createJob connString groupId requestUrl fhirTypes
    // To:
    //   let job = BulkExport.createJob groupId requestUrl fhirTypes
```

`bulkExportKickoff` handler — update the `runExport` call (remove `connString` as first arg):
```fsharp
    // Change:
    //   let _ = Task.Run(fun () -> BulkExport.runExport connString job groupJson :> Task)
    // To:
    //   let _ = Task.Run(fun () -> BulkExport.runExport connString job groupJson :> Task)
```
Note: `runExport` still takes `connString` because it calls `Repository` functions directly. No change needed here.

`bulkStatusDelete` handler — update the `expireJob` call (remove `connString`, no longer async):
```fsharp
    // Change:
    //   let! expired = BulkExport.expireJob connString jobId
    // To:
    //   let expired = BulkExport.expireJob jobId
```

- [ ] **Step 5: Build to verify**

Run: `dotnet build BulkFhir.slnx`
Expected: Build succeeded. 0 Error(s)

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Remove dashboard DB schema and bulk export job persistence"
```

---

### Task 3: Add OTEL NuGet packages

**Files:**
- Modify: `src/BulkFhir.Api/BulkFhir.Api.fsproj`

- [ ] **Step 1: Add 6 OTEL packages**

Run these commands:
```bash
cd src/BulkFhir.Api
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
dotnet add package OpenTelemetry.Instrumentation.Http
dotnet add package OpenTelemetry.Instrumentation.Runtime
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package Npgsql.OpenTelemetry
```

- [ ] **Step 2: Build to verify packages resolve**

Run: `dotnet build BulkFhir.slnx`
Expected: Build succeeded. 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add src/BulkFhir.Api/BulkFhir.Api.fsproj
git commit -m "Add OpenTelemetry NuGet packages"
```

---

### Task 4: Wire OTEL in Program.fs

**Files:**
- Modify: `src/BulkFhir.Api/Program.fs`

- [ ] **Step 1: Add OTEL configuration**

Add these opens at the top of `src/BulkFhir.Api/Program.fs`:
```fsharp
open OpenTelemetry
open OpenTelemetry.Metrics
open OpenTelemetry.Trace
open OpenTelemetry.Logs
open OpenTelemetry.Resources
```

Between `builder.Services.AddRouting()` and `let app = builder.Build()`, add:
```fsharp
    let otelServiceName =
        Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME")
        |> Option.ofObj
        |> Option.defaultValue "bulk-fhir"

    builder.Services
        .AddOpenTelemetry()
        .ConfigureResource(fun r -> r.AddService(otelServiceName) |> ignore)
        .WithMetrics(fun m ->
            m.AddAspNetCoreInstrumentation()
             .AddHttpClientInstrumentation()
             .AddRuntimeInstrumentation()
             .AddMeter("Npgsql")
             |> ignore)
        .WithTracing(fun t ->
            t.AddAspNetCoreInstrumentation()
             .AddHttpClientInstrumentation()
             .AddNpgsql()
             |> ignore)
        .UseOtlpExporter()
        |> ignore

    builder.Logging.AddOpenTelemetry(fun o ->
        o.IncludeScopes <- true
        o.IncludeFormattedMessage <- true
        o.SetResourceBuilder(
            ResourceBuilder.CreateDefault().AddService(otelServiceName))
        |> ignore) |> ignore
```

Also add at the top:
```fsharp
open Microsoft.Extensions.Logging
```

Note: `AddNpgsql()` is resolved via `open OpenTelemetry` (already in the opens above). Do NOT add `open Npgsql.OpenTelemetry` — that namespace does not exist.

- [ ] **Step 2: Build to verify**

Run: `dotnet build BulkFhir.slnx`
Expected: Build succeeded. 0 Error(s)

- [ ] **Step 3: Commit**

```bash
git add src/BulkFhir.Api/Program.fs
git commit -m "Wire OpenTelemetry metrics, traces, and logs in Program.fs"
```

---

### Task 5: Add docker-compose.yml for local Phoenix

**Files:**
- Create: `docker-compose.yml`

- [ ] **Step 1: Create docker-compose.yml**

Create `docker-compose.yml` in the repo root:
```yaml
services:
  phoenix:
    image: arizephoenix/phoenix:latest
    ports:
      - "6006:6006"
      - "4317:4317"
```

- [ ] **Step 2: Commit**

```bash
git add docker-compose.yml
git commit -m "Add docker-compose for local Phoenix OTEL collector"
```

---

### Task 6: Run locally and verify end-to-end

- [ ] **Step 1: Start Phoenix**

```bash
docker compose up -d
```

Expected: Phoenix container starts, UI at http://localhost:6006

- [ ] **Step 2: Run the app with OTEL enabled**

```bash
OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317 \
OTEL_EXPORTER_OTLP_PROTOCOL=grpc \
OTEL_SERVICE_NAME=bulk-fhir \
CONNECTION_STRING="<your-connection-string>" \
dotnet run --project src/BulkFhir.Api
```

- [ ] **Step 3: Generate some traffic**

```bash
curl http://localhost:5000/health
curl http://localhost:5000/fhir/metadata
curl http://localhost:5000/fhir/Patient?_count=5
```

- [ ] **Step 4: Verify in Phoenix UI**

Open http://localhost:6006 in browser. Check:
- Traces tab: should show HTTP request spans with child Npgsql spans
- Metrics: should show ASP.NET Core HTTP metrics

- [ ] **Step 5: Stop Phoenix**

```bash
docker compose down
```
