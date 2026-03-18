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

    let private statusToText status =
        match status with
        | Pending -> "pending"
        | InProgress _ -> "in_progress"
        | Completed -> "completed"
        | Failed _ -> "failed"
        | Expired -> "expired"

    let private progressText status =
        match status with
        | InProgress progress -> Some progress
        | Failed error -> Some error
        | _ -> None

    let private typesToText (types: FhirResourceType list) =
        types |> List.map FhirResourceType.toString |> String.concat ","

    let private persistJob (connString: string) (job: ExportJob) =
        Repository.upsertBulkExportJob
            connString
            job.Id
            job.GroupId
            (statusToText job.Status)
            job.RequestUrl
            (typesToText job.Types)
            job.CreatedAt
            job.CompletedAt
            job.ExpiresAt
            (progressText job.Status)

    let createJob (connString: string) (groupId: string) (requestUrl: string) (types: FhirResourceType list) =
        task {
            let jobId = Guid.NewGuid().ToString("N").[..7]
            let jobDir = Path.Combine(exportDir, jobId)
            Directory.CreateDirectory(jobDir) |> ignore
            let job =
                { Id = jobId; GroupId = groupId; RequestUrl = requestUrl
                  Types = types; Status = Pending; CreatedAt = DateTime.UtcNow
                  CompletedAt = None; ExpiresAt = None; OutputDir = jobDir }
            jobs.[jobId] <- job
            do! persistJob connString job
            return job
        }

    let getJob (jobId: string) =
        match jobs.TryGetValue(jobId) with
        | true, job -> Some job
        | _ -> None

    let updateJob (connString: string) (job: ExportJob) =
        task {
            jobs.[job.Id] <- job
            do! persistJob connString job
        }

    let expireJob (connString: string) (jobId: string) =
        task {
            match jobs.TryGetValue(jobId) with
            | true, job ->
                let expiredJob = { job with Status = Expired; ExpiresAt = Some DateTime.UtcNow }
                jobs.[job.Id] <- expiredJob
                do! persistJob connString expiredJob
                try
                    if Directory.Exists(job.OutputDir) then
                        Directory.Delete(job.OutputDir, true)
                with _ ->
                    ()
                return true
            | _ -> return false
        }

    /// Run the bulk export: query resources for each type linked to the group, write NDJSON files.
    let runExport (connString: string) (job: ExportJob) (groupJson: string) =
        task {
            try
                do! updateJob connString { job with Status = InProgress "Resolving group members..." }

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

                do! updateJob connString { job with Status = InProgress $"Found {patientRefs.Length} members, exporting..." }

                // Write Group NDJSON
                if job.Types |> List.contains FhirResourceType.Group then
                    let path = Path.Combine(job.OutputDir, "Group-1.ndjson")
                    File.WriteAllText(path, groupJson + "\n")

                // For each requested type, query resources and write NDJSON
                for rt in job.Types do
                    if rt <> FhirResourceType.Group then
                        let typeName = FhirResourceType.toString rt
                        do! updateJob connString { job with Status = InProgress $"Exporting {typeName}..." }

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
                do! updateJob connString { job with Status = Completed; CompletedAt = Some now; ExpiresAt = Some (now.AddHours(1.0)) }
            with ex ->
                do! updateJob connString { job with Status = Failed ex.Message }
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
