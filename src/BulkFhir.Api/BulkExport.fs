namespace BulkFhir.Api

open System
open System.IO
open System.Collections.Concurrent
open System.Text.Json
open BulkFhir.Domain
open BulkFhir.Storage

/// In-memory bulk export job management.
/// Jobs write NDJSON files to a temp directory for download.
module BulkExport =

    type JobStatus = Pending | InProgress of progress: string | Completed | Failed of error: string | Expired

    type ExportJob =
        { Id: string
          GroupId: string
          RequestUrl: string
          Types: FhirResourceType list
          Status: JobStatus
          CreatedAt: DateTime
          CompletedAt: DateTime option
          ExpiresAt: DateTime option
          OutputDir: string }

    let private jobs = ConcurrentDictionary<string, ExportJob>()

    let private exportDir =
        let dir = Path.Combine(Path.GetTempPath(), "bulk-fhir-exports")
        Directory.CreateDirectory(dir) |> ignore
        dir

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

    let getJob (jobId: string) =
        match jobs.TryGetValue(jobId) with
        | true, job -> Some job
        | _ -> None

    let updateJob (job: ExportJob) =
        jobs.[job.Id] <- job

    let expireJob (jobId: string) =
        match jobs.TryGetValue(jobId) with
        | true, job ->
            jobs.[job.Id] <- { job with Status = Expired; ExpiresAt = Some DateTime.UtcNow }
            try if Directory.Exists(job.OutputDir) then Directory.Delete(job.OutputDir, true)
            with _ -> ()
            true
        | _ -> false

    /// Run the bulk export: query resources for each type linked to the group, write NDJSON files.
    let runExport (connString: string) (job: ExportJob) (groupJson: string) =
        task {
            try
                updateJob { job with Status = InProgress "Resolving group members..." }

                let doc = JsonDocument.Parse(groupJson)
                let root = doc.RootElement

                // Extract patient references from Group.member[].entity.reference
                let patientRefs =
                    match root.TryGetProperty("member") with
                    | true, arr when arr.ValueKind = JsonValueKind.Array ->
                        [ for i in 0 .. arr.GetArrayLength() - 1 do
                            let m = arr.[i]
                            match m.TryGetProperty("entity") with
                            | true, e ->
                                match e.TryGetProperty("reference") with
                                | true, r when r.ValueKind = JsonValueKind.String ->
                                    yield r.GetString()
                                | _ -> ()
                            | _ -> () ]
                    | _ -> []

                // Normalize refs: extract the UUID from both "Patient/uuid" and "urn:uuid:uuid"
                let patientIds =
                    patientRefs |> List.map (fun r ->
                        if r.StartsWith("Patient/") then r.[8..]
                        elif r.StartsWith("urn:uuid:") then r.[9..]
                        else r)

                // Build the "Patient/{id}" form for subject_ref lookups
                let patientSubjectRefs = patientIds |> List.map (fun id -> $"Patient/{id}")

                updateJob { job with Status = InProgress $"Found {patientRefs.Length} members, exporting..." }

                // Write Group NDJSON
                if job.Types |> List.contains FhirResourceType.Group then
                    let path = Path.Combine(job.OutputDir, "Group-1.ndjson")
                    File.WriteAllText(path, groupJson + "\n")

                // For each requested type, query resources and write NDJSON
                for rt in job.Types do
                    if rt <> FhirResourceType.Group then
                        let typeName = FhirResourceType.toString rt
                        updateJob { job with Status = InProgress $"Exporting {typeName}..." }

                        match rt with
                        | FhirResourceType.Organization | FhirResourceType.Practitioner ->
                            let path = Path.Combine(job.OutputDir, $"{typeName}-1.ndjson")
                            let! count = Repository.streamAllToFile connString rt path
                            if count = 0 then File.Delete(path)
                        | _ ->
                            let! resources =
                                match rt with
                                | FhirResourceType.Patient ->
                                    Repository.readByIds connString FhirResourceType.Patient patientIds
                                | _ ->
                                    Repository.getBySubjectRefs connString rt patientSubjectRefs
                            if not resources.IsEmpty then
                                let path = Path.Combine(job.OutputDir, $"{typeName}-1.ndjson")
                                let content = resources |> String.concat "\n"
                                File.WriteAllText(path, content + "\n")

                let now = DateTime.UtcNow
                updateJob { job with Status = Completed; CompletedAt = Some now; ExpiresAt = Some (now.AddHours(1.0)) }
            with ex ->
                updateJob { job with Status = Failed ex.Message }
        }

    /// Build the export manifest response for a completed job.
    let buildManifest (baseUrl: string) (job: ExportJob) =
        let files = Directory.GetFiles(job.OutputDir, "*.ndjson")
        let output =
            files
            |> Array.map (fun f ->
                let fileName = Path.GetFileName(f)
                let typeName = fileName.Split('-').[0]
                {| ``type`` = typeName; url = $"{baseUrl}/fhir/bulk-files/{job.Id}/{fileName}" |})

        JsonSerializer.Serialize(
            {| transactionTime = (job.CompletedAt |> Option.defaultValue DateTime.UtcNow).ToString("o")
               request = job.RequestUrl
               requiresAccessToken = false
               output = output
               error = Array.empty<string> |},
            JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase))
