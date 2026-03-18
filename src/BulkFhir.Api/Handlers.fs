namespace BulkFhir.Api

open System
open System.IO
open System.Threading.Tasks
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Falco
open BulkFhir.Domain
open BulkFhir.Storage

/// HTTP handlers for all FHIR endpoints.
module Handlers =

    let private getConnString (ctx: HttpContext) =
        ctx.RequestServices.GetService(typeof<IConfiguration>) :?> IConfiguration
        |> fun c -> c.GetConnectionString("DefaultConnection")

    let private getBaseUrl (ctx: HttpContext) =
        $"{ctx.Request.Scheme}://{ctx.Request.Host}"

    let private fhirJson (statusCode: int) (body: string) : HttpHandler =
        fun ctx ->
            ctx.Response.StatusCode <- statusCode
            ctx.Response.ContentType <- Fhir.fhirContentType
            ctx.Response.WriteAsync(body)

    // ─── Health ─────────────────────────────────────

    let health : HttpHandler =
        fun ctx ->
            task {
                try
                    let connString = getConnString ctx
                    use conn = new Npgsql.NpgsqlConnection(connString)
                    do! conn.OpenAsync()
                    ctx.Response.ContentType <- "application/json"
                    return! ctx.Response.WriteAsJsonAsync({| status = "ok" |})
                with ex ->
                    ctx.Response.StatusCode <- 503
                    return! ctx.Response.WriteAsJsonAsync({| status = "error"; detail = ex.Message |})
            } :> Task

    // ─── Metadata ───────────────────────────────────

    let metadata : HttpHandler =
        fun ctx ->
            let baseUrl = getBaseUrl ctx
            fhirJson 200 (Fhir.capabilityStatement baseUrl) ctx

    // ─── Group ──────────────────────────────────────

    let groupSearch : HttpHandler =
        fun ctx ->
            task {
                let connString = getConnString ctx
                let baseUrl = getBaseUrl ctx
                let query = ctx.Request.Query

                let knownKeys = set ["identifier"; "name"]
                let unknown =
                    query.Keys
                    |> Seq.filter (fun k -> not (knownKeys.Contains k))
                    |> Seq.toList
                if not unknown.IsEmpty then
                    let msg = sprintf "Unsupported search parameter(s) for Group: %s" (String.Join(", ", unknown))
                    return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" msg) ctx
                else

                let hasIdentifier = query.ContainsKey("identifier")
                let hasName = query.ContainsKey("name")

                if hasIdentifier && hasName then
                    return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" "Provide either 'identifier' or 'name', not both.") ctx
                elif not hasIdentifier && not hasName then
                    return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" "Provide either 'identifier' or 'name'.") ctx
                elif hasIdentifier then
                    let raw = string query.["identifier"]
                    match raw.IndexOf('|') with
                    | -1 ->
                        return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" "identifier must be in format system|value") ctx
                    | i ->
                        let system = raw.[0..i-1]
                        let value = raw.[i+1..]
                        let! results = Repository.searchGroupsByIdentifier connString system value
                        let selfUrl = $"{baseUrl}/fhir/Group?identifier={Uri.EscapeDataString raw}"
                        return! fhirJson 200 (Fhir.searchBundle baseUrl "Group" selfUrl results) ctx
                else
                    let name = string query.["name"]
                    let! results = Repository.searchGroupsByName connString name
                    let selfUrl = $"{baseUrl}/fhir/Group?name={Uri.EscapeDataString name}"
                    return! fhirJson 200 (Fhir.searchBundle baseUrl "Group" selfUrl results) ctx
            } :> Task

    let groupRead : HttpHandler =
        fun ctx ->
            task {
                let connString = getConnString ctx
                let id = ctx.Request.RouteValues.["id"] :?> string
                let! result = Repository.readById connString FhirResourceType.Group id
                match result with
                | Some json -> return! fhirJson 200 json ctx
                | None -> return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" "Group was not found.") ctx
            } :> Task

    // ─── Bulk Export ────────────────────────────────

    let private validExportTypes =
        FhirResourceType.all |> List.map FhirResourceType.toString |> Set.ofList

    let bulkExportKickoff : HttpHandler =
        fun ctx ->
            task {
                let connString = getConnString ctx
                let baseUrl = getBaseUrl ctx
                let query = ctx.Request.Query
                let groupId = ctx.Request.RouteValues.["id"] :?> string

                // Validate exportType
                let exportType = if query.ContainsKey("exportType") then Some (string query.["exportType"]) else None
                match exportType with
                | None | Some "" ->
                    return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" "exportType is required.") ctx
                | Some et when et <> "hl7.fhir.us.davinci-atr" ->
                    return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" $"exportType must be 'hl7.fhir.us.davinci-atr', got '{et}'.") ctx
                | _ ->

                // Validate _type
                let typeParam =
                    if query.ContainsKey("_type") then Some (string query.["_type"])
                    elif query.ContainsKey("resourceTypes") then Some (string query.["resourceTypes"])
                    else None

                match typeParam with
                | None | Some "" ->
                    return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" "_type is required.") ctx
                | Some types ->

                let requestedTypes = types.Split(',') |> Array.map (fun s -> s.Trim()) |> Array.toList
                let invalid = requestedTypes |> List.filter (fun t -> not (validExportTypes.Contains t))
                if not invalid.IsEmpty then
                    let msg = sprintf "Unsupported resource types: %s" (String.Join(", ", invalid))
                    return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" msg) ctx
                else

                // Check group exists
                let! group = Repository.readById connString FhirResourceType.Group groupId
                match group with
                | None ->
                    return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" "Group not found.") ctx
                | Some groupJson ->

                let fhirTypes =
                    requestedTypes
                    |> List.choose FhirResourceType.fromString

                let requestUrl = $"{baseUrl}{ctx.Request.Path}{ctx.Request.QueryString}"
                let job = BulkExport.createJob groupId requestUrl fhirTypes

                // Fire and forget the export (pass pre-fetched group JSON to avoid re-read)
                let _ = Task.Run(fun () -> BulkExport.runExport connString job groupJson :> Task)

                ctx.Response.StatusCode <- 202
                ctx.Response.Headers.["Content-Location"] <- $"{baseUrl}/fhir/bulk-status/{job.Id}"
                ctx.Response.Headers.["Retry-After"] <- "1"
                return ()
            } :> Task

    let bulkStatusPoll : HttpHandler =
        fun ctx ->
            task {
                let baseUrl = getBaseUrl ctx
                let jobId = ctx.Request.RouteValues.["jobId"] :?> string

                match BulkExport.getJob jobId with
                | None ->
                    return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" "Job not found or expired.") ctx
                | Some job ->

                match job.Status with
                | BulkExport.Expired ->
                    return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" "Job expired.") ctx
                | BulkExport.Failed err ->
                    return! fhirJson 500 (Fhir.operationOutcome "error" "exception" err) ctx
                | BulkExport.Completed ->
                    let manifest = BulkExport.buildManifest baseUrl job
                    match job.ExpiresAt with
                    | Some exp -> ctx.Response.Headers.["Expires"] <- exp.ToString("R")
                    | None -> ()
                    return! fhirJson 200 manifest ctx
                | BulkExport.Pending | BulkExport.InProgress _ ->
                    let progress =
                        match job.Status with
                        | BulkExport.InProgress p -> p
                        | _ -> "Queued"
                    ctx.Response.StatusCode <- 202
                    ctx.Response.Headers.["X-Progress"] <- progress
                    ctx.Response.Headers.["Retry-After"] <- "1"
                    return ()
            } :> Task

    let bulkStatusDelete : HttpHandler =
        fun ctx ->
            task {
                let jobId = ctx.Request.RouteValues.["jobId"] :?> string
                if BulkExport.expireJob jobId then
                    ctx.Response.StatusCode <- 202
                    return ()
                else
                    return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" "Job not found.") ctx
            } :> Task

    let bulkFileDownload : HttpHandler =
        fun ctx ->
            task {
                let jobId = ctx.Request.RouteValues.["jobId"] :?> string
                let fileName = ctx.Request.RouteValues.["fileName"] :?> string

                let sanitized = Path.GetFileName(fileName)
                if sanitized <> fileName then
                    return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" "Invalid file name.") ctx
                else

                match BulkExport.getJob jobId with
                | None ->
                    return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" "Job not found.") ctx
                | Some job ->
                    let filePath = Path.Combine(job.OutputDir, sanitized)
                    if File.Exists(filePath) then
                        ctx.Response.ContentType <- Fhir.ndjsonContentType
                        use fileStream = File.OpenRead(filePath)
                        do! fileStream.CopyToAsync(ctx.Response.Body)
                    else
                        return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" $"File '{fileName}' not found.") ctx
            } :> Task

    // ─── Generic Resource Read/Search ───────────────

    let resourceSearch : HttpHandler =
        fun ctx ->
            task {
                let connString = getConnString ctx
                let baseUrl = getBaseUrl ctx
                let typeName = ctx.Request.RouteValues.["resourceType"] :?> string

                match FhirResourceType.fromString typeName with
                | None ->
                    return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" $"Unknown resource type: {typeName}") ctx
                | Some rt ->
                    let queryParams =
                        ctx.Request.Query
                        |> Seq.collect (fun kv -> kv.Value |> Seq.map (fun v -> kv.Key, v))
                        |> Seq.toList

                    let known = Repository.knownSearchParams rt
                    let unknown =
                        queryParams
                        |> List.map fst
                        |> List.filter (fun k -> not (known.Contains k))
                        |> List.distinct

                    if not unknown.IsEmpty then
                        let msg = sprintf "Unsupported search parameter(s) for %s: %s" typeName (String.Join(", ", unknown))
                        return! fhirJson 400 (Fhir.operationOutcome "error" "invalid" msg) ctx
                    else

                    let searchParams = Repository.parseSearchParams rt queryParams
                    let! results = Repository.search connString rt searchParams
                    let selfUrl = $"{baseUrl}{ctx.Request.Path}{ctx.Request.QueryString}"
                    return! fhirJson 200 (Fhir.searchBundle baseUrl typeName selfUrl results) ctx
            } :> Task

    let resourceRead : HttpHandler =
        fun ctx ->
            task {
                let connString = getConnString ctx
                let typeName = ctx.Request.RouteValues.["resourceType"] :?> string
                let id = ctx.Request.RouteValues.["id"] :?> string

                match FhirResourceType.fromString typeName with
                | None ->
                    return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" $"Unknown resource type: {typeName}") ctx
                | Some rt ->
                    let! result = Repository.readById connString rt id
                    match result with
                    | Some json -> return! fhirJson 200 json ctx
                    | None -> return! fhirJson 404 (Fhir.operationOutcome "error" "not-found" $"{typeName} was not found.") ctx
            } :> Task
