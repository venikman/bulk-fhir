namespace BulkFhir.Storage

open System
open System.Globalization
open Npgsql
open Dapper
open BulkFhir.Domain

/// Repository for FHIR resource CRUD and search.
/// All reads return raw JSON text (resource_text column) for FHIR-compliant responses.
module Repository =

    /// Read multiple resources by IDs in a single batch query, returning raw JSON.
    let readByIds (connString: string) (resourceType: FhirResourceType) (ids: string list) =
        task {
            if ids.IsEmpty then return []
            else
                let table = FhirResourceType.tableName resourceType
                let sql = $"SELECT resource_text FROM {table} WHERE id = ANY(@Ids)"
                use conn = Connection.createConnection connString
                let! results = conn.QueryAsync<string>(sql, {| Ids = ids |> Array.ofList |})
                return results |> Seq.toList
        }

    /// Read a single resource by ID, returning the raw JSON.
    let readById (connString: string) (resourceType: FhirResourceType) (id: string) =
        task {
            let table = FhirResourceType.tableName resourceType
            let sql = $"SELECT resource_text FROM {table} WHERE id = @Id LIMIT 1"
            use conn = Connection.createConnection connString
            let! result = conn.QuerySingleOrDefaultAsync<string>(sql, {| Id = id |})
            return if isNull result then None else Some result
        }

    /// Search resources returning raw JSON + total count. Applies WHERE clause from search params.
    let search (connString: string) (resourceType: FhirResourceType) (searchParams: Search.SearchParam list) (count: int) (offset: int) =
        task {
            let table = FhirResourceType.tableName resourceType
            let whereClause, parameters = Search.buildWhere searchParams

            use conn = Connection.createConnection connString
            let dynParams = DynamicParameters()
            for (name, value) in parameters do
                dynParams.Add(name, value)

            let countSql = $"SELECT count(*)::int FROM {table}{whereClause}"
            let! total = conn.QuerySingleAsync<int>(countSql, dynParams)

            let sql = $"SELECT resource_text FROM {table}{whereClause} ORDER BY id LIMIT {count} OFFSET {offset}"
            let! results = conn.QueryAsync<string>(sql, dynParams)
            return total, results |> Seq.toList
        }

    /// Stream all resources of a type to a file, returning count written.
    let streamAllToFile (connString: string) (resourceType: FhirResourceType) (filePath: string) =
        task {
            let table = FhirResourceType.tableName resourceType
            let sql = $"SELECT resource_text FROM {table}"
            use conn = Connection.createConnection connString
            do! conn.OpenAsync()
            use cmd = new NpgsqlCommand(sql, conn)
            use! reader = cmd.ExecuteReaderAsync()
            use writer = new System.IO.StreamWriter(filePath)
            let mutable count = 0
            while! reader.ReadAsync() do
                do! writer.WriteLineAsync(reader.GetString(0))
                count <- count + 1
            return count
        }

    /// List all resources of a type. For small result sets only (Groups, search results).
    let listAll (connString: string) (resourceType: FhirResourceType) =
        task {
            let table = FhirResourceType.tableName resourceType
            let sql = $"SELECT resource_text FROM {table}"
            use conn = Connection.createConnection connString
            let! results = conn.QueryAsync<string>(sql)
            return results |> Seq.toList
        }

    /// Search groups by identifier (system|value).
    let searchGroupsByIdentifier (connString: string) (system: string) (value: string) =
        task {
            let sql = "SELECT resource_text FROM groups WHERE identifier_system = @System AND identifier_value = @Value"
            use conn = Connection.createConnection connString
            let! results = conn.QueryAsync<string>(sql, {| System = system; Value = value |})
            return results |> Seq.toList
        }

    /// Search groups by name (case-insensitive partial match).
    let searchGroupsByName (connString: string) (name: string) =
        task {
            let sql = "SELECT resource_text FROM groups WHERE lower(name) LIKE @Name"
            use conn = Connection.createConnection connString
            let! results = conn.QueryAsync<string>(sql, {| Name = $"%%{name.ToLowerInvariant()}%%" |})
            return results |> Seq.toList
        }

    /// Persist the dashboard-visible state for a bulk export job.
    let upsertBulkExportJob
        (connString: string)
        (jobId: string)
        (groupId: string)
        (status: string)
        (requestUrl: string)
        (types: string)
        (createdAt: DateTime)
        (completedAt: DateTime option)
        (expiresAt: DateTime option)
        (progress: string option) =
        task {
            let sql =
                """
                INSERT INTO bulk_export_jobs (
                    id, group_id, status, request_url, types, created_at, completed_at, expires_at, progress
                )
                VALUES (
                    @Id, @GroupId, @Status, @RequestUrl, @Types, @CreatedAt, @CompletedAt, @ExpiresAt, @Progress
                )
                ON CONFLICT (id) DO UPDATE SET
                    group_id = EXCLUDED.group_id,
                    status = EXCLUDED.status,
                    request_url = EXCLUDED.request_url,
                    types = EXCLUDED.types,
                    created_at = EXCLUDED.created_at,
                    completed_at = EXCLUDED.completed_at,
                    expires_at = EXCLUDED.expires_at,
                    progress = EXCLUDED.progress
                """

            use conn = Connection.createConnection connString
            let completedAtValue = completedAt |> Option.toNullable
            let expiresAtValue = expiresAt |> Option.toNullable
            let progressValue = progress |> Option.toObj
            let! _ =
                conn.ExecuteAsync(
                    sql,
                    {| Id = jobId
                       GroupId = groupId
                       Status = status
                       RequestUrl = requestUrl
                       Types = types
                       CreatedAt = createdAt
                       CompletedAt = completedAtValue
                       ExpiresAt = expiresAtValue
                       Progress = progressValue |})

            return ()
        }

    /// Get all resource_text for a given type and list of subject/patient references.
    /// Used by bulk export to resolve references from a Group.
    let getBySubjectRefs (connString: string) (resourceType: FhirResourceType) (refs: string list) =
        task {
            if refs.IsEmpty then return []
            else
                let table = FhirResourceType.tableName resourceType
                let refCol =
                    match resourceType with
                    | FhirResourceType.AllergyIntolerance
                    | FhirResourceType.Immunization
                    | FhirResourceType.Claim
                    | FhirResourceType.ExplanationOfBenefit
                    | FhirResourceType.Device -> "patient_ref"
                    | FhirResourceType.Encounter
                    | FhirResourceType.Condition
                    | FhirResourceType.Observation
                    | FhirResourceType.Procedure
                    | FhirResourceType.MedicationRequest
                    | FhirResourceType.CarePlan
                    | FhirResourceType.CareTeam
                    | FhirResourceType.DiagnosticReport
                    | FhirResourceType.DocumentReference
                    | FhirResourceType.ImagingStudy -> "subject_ref"
                    | FhirResourceType.Patient
                    | FhirResourceType.Practitioner
                    | FhirResourceType.Organization
                    | FhirResourceType.Group ->
                        failwith $"getBySubjectRefs not applicable to {FhirResourceType.toString resourceType}"

                let sql = $"SELECT resource_text FROM {table} WHERE {refCol} = ANY(@Refs)"
                use conn = Connection.createConnection connString
                let! results = conn.QueryAsync<string>(sql, {| Refs = refs |> Array.ofList |})
                return results |> Seq.toList
        }

    /// Known search parameters per resource type (including meta-params).
    /// Used by the handler to reject unsupported parameters.
    let knownSearchParams (resourceType: FhirResourceType) : Set<string> =
        let metaParams = set ["_summary"; "_count"; "_offset"; "_format"]
        let typeParams =
            match resourceType with
            | FhirResourceType.Patient              -> set ["name"; "birthdate"; "gender"; "general-practitioner"]
            | FhirResourceType.Encounter            -> set ["patient"; "date"; "status"; "type"; "practitioner"; "reason-code"]
            | FhirResourceType.Condition            -> set ["patient"; "code"; "clinical-status"; "category"]
            | FhirResourceType.Observation          -> set ["patient"; "code"; "category"; "date"]
            | FhirResourceType.Procedure            -> set ["patient"; "code"]
            | FhirResourceType.MedicationRequest    -> set ["patient"; "code"; "status"]
            | FhirResourceType.AllergyIntolerance   -> set ["patient"; "code"; "clinical-status"]
            | FhirResourceType.Immunization         -> set ["patient"; "status"; "date"]
            | FhirResourceType.CarePlan             -> set ["patient"; "status"]
            | FhirResourceType.CareTeam             -> set ["patient"; "status"]
            | FhirResourceType.Claim                -> set ["patient"; "status"; "created"]
            | FhirResourceType.ExplanationOfBenefit -> set ["patient"; "status"; "created"]
            | FhirResourceType.DiagnosticReport     -> set ["patient"; "code"; "date"; "status"]
            | FhirResourceType.DocumentReference    -> set ["patient"; "status"; "date"]
            | FhirResourceType.Device               -> set ["patient"; "status"]
            | FhirResourceType.ImagingStudy         -> set ["patient"; "status"; "started"]
            | FhirResourceType.Organization         -> set ["name"]
            | FhirResourceType.Practitioner         -> set ["name"]
            | FhirResourceType.Group                -> set []
        Set.union metaParams typeParams

    /// Parse FHIR search query params into SearchParam list per resource type.
    let parseSearchParams (resourceType: FhirResourceType) (queryParams: (string * string) list) : Search.SearchParam list =
        queryParams
        |> List.choose (fun (key, value) ->
            // Skip meta-parameters
            match key with
            | "_summary" | "_count" | "_format" -> None
            | _ ->

            match resourceType, key with
            // Patient
            | FhirResourceType.Patient, "name" ->
                Some (Search.OrStringParam (["family_name"; "given_name"], value))
            | FhirResourceType.Patient, "birthdate" ->
                let prefix, dateStr = Search.parseDatePrefix value
                match DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, dt -> Some (Search.DateParam ("birth_date", prefix, dt))
                | _ -> None
            | FhirResourceType.Patient, "gender" ->
                Some (Search.ReferenceParam ("gender", value))
            | FhirResourceType.Patient, "general-practitioner" -> None // Column does not exist; silently skip (known limitation)

            // Encounter
            | FhirResourceType.Encounter, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.Encounter, "date" ->
                let prefix, dateStr = Search.parseDatePrefix value
                match DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, dt -> Some (Search.DateParam ("period_start", prefix, dt))
                | _ -> None
            | FhirResourceType.Encounter, "status" ->
                Some (Search.ReferenceParam ("status", value))
            | FhirResourceType.Encounter, "type" ->
                let sys, code = Search.parseToken value
                Some (Search.TokenParam ("type_system", "type_code", sys, code))
            | FhirResourceType.Encounter, "practitioner" ->
                Some (Search.ReferenceParam ("practitioner_ref", value))
            | FhirResourceType.Encounter, "reason-code" ->
                let sys, code = Search.parseToken value
                Some (Search.TokenParam ("reason_system", "reason_code", sys, code))

            // Condition
            | FhirResourceType.Condition, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.Condition, "code" ->
                let sys, code = Search.parseToken value
                Some (Search.TokenParam ("code_system", "code_value", sys, code))
            | FhirResourceType.Condition, "clinical-status" ->
                let _, code = Search.parseToken value
                Some (Search.ReferenceParam ("clinical_status", code))
            | FhirResourceType.Condition, "category" ->
                Some (Search.ReferenceParam ("category_code", value))

            // Observation
            | FhirResourceType.Observation, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.Observation, "code" ->
                let sys, code = Search.parseToken value
                Some (Search.TokenParam ("code_system", "code_value", sys, code))
            | FhirResourceType.Observation, "category" ->
                Some (Search.ReferenceParam ("category_code", value))
            | FhirResourceType.Observation, "date" ->
                let prefix, dateStr = Search.parseDatePrefix value
                match DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, dt -> Some (Search.DateParam ("effective_date", prefix, dt))
                | _ -> None

            // Procedure
            | FhirResourceType.Procedure, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.Procedure, "code" ->
                let sys, code = Search.parseToken value
                Some (Search.TokenParam ("code_system", "code_value", sys, code))

            // MedicationRequest
            | FhirResourceType.MedicationRequest, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.MedicationRequest, "code" ->
                let sys, code = Search.parseToken value
                Some (Search.TokenParam ("code_system", "code_value", sys, code))
            | FhirResourceType.MedicationRequest, "status" ->
                Some (Search.ReferenceParam ("status", value))

            // AllergyIntolerance
            | FhirResourceType.AllergyIntolerance, "patient" ->
                Some (Search.ReferenceParam ("patient_ref", value))
            | FhirResourceType.AllergyIntolerance, "code" ->
                let sys, code = Search.parseToken value
                Some (Search.TokenParam ("code_system", "code_value", sys, code))
            | FhirResourceType.AllergyIntolerance, "clinical-status" ->
                let _, code = Search.parseToken value
                Some (Search.ReferenceParam ("clinical_status", code))

            // Immunization
            | FhirResourceType.Immunization, "patient" ->
                Some (Search.ReferenceParam ("patient_ref", value))
            | FhirResourceType.Immunization, "status" ->
                Some (Search.ReferenceParam ("status", value))
            | FhirResourceType.Immunization, "date" ->
                let prefix, dateStr = Search.parseDatePrefix value
                match DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, dt -> Some (Search.DateParam ("occurrence_date", prefix, dt))
                | _ -> None

            // CarePlan
            | FhirResourceType.CarePlan, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.CarePlan, "status" ->
                Some (Search.ReferenceParam ("status", value))

            // CareTeam
            | FhirResourceType.CareTeam, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.CareTeam, "status" ->
                Some (Search.ReferenceParam ("status", value))

            // Claim
            | FhirResourceType.Claim, "patient" ->
                Some (Search.ReferenceParam ("patient_ref", value))
            | FhirResourceType.Claim, "status" ->
                Some (Search.ReferenceParam ("status", value))
            | FhirResourceType.Claim, "created" ->
                let prefix, dateStr = Search.parseDatePrefix value
                match DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, dt -> Some (Search.DateParam ("created", prefix, dt))
                | _ -> None

            // ExplanationOfBenefit
            | FhirResourceType.ExplanationOfBenefit, "patient" ->
                Some (Search.ReferenceParam ("patient_ref", value))
            | FhirResourceType.ExplanationOfBenefit, "status" ->
                Some (Search.ReferenceParam ("status", value))
            | FhirResourceType.ExplanationOfBenefit, "created" ->
                let prefix, dateStr = Search.parseDatePrefix value
                match DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, dt -> Some (Search.DateParam ("created", prefix, dt))
                | _ -> None

            // DiagnosticReport
            | FhirResourceType.DiagnosticReport, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.DiagnosticReport, "code" ->
                let sys, code = Search.parseToken value
                Some (Search.TokenParam ("code_system", "code_value", sys, code))
            | FhirResourceType.DiagnosticReport, "date" ->
                let prefix, dateStr = Search.parseDatePrefix value
                match DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, dt -> Some (Search.DateParam ("effective_date", prefix, dt))
                | _ -> None
            | FhirResourceType.DiagnosticReport, "status" ->
                Some (Search.ReferenceParam ("status", value))

            // DocumentReference
            | FhirResourceType.DocumentReference, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.DocumentReference, "status" ->
                Some (Search.ReferenceParam ("status", value))
            | FhirResourceType.DocumentReference, "date" ->
                let prefix, dateStr = Search.parseDatePrefix value
                match DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, dt -> Some (Search.DateParam ("date", prefix, dt))
                | _ -> None

            // Device
            | FhirResourceType.Device, "patient" ->
                Some (Search.ReferenceParam ("patient_ref", value))
            | FhirResourceType.Device, "status" ->
                Some (Search.ReferenceParam ("status", value))

            // ImagingStudy
            | FhirResourceType.ImagingStudy, "patient" ->
                Some (Search.ReferenceParam ("subject_ref", value))
            | FhirResourceType.ImagingStudy, "status" ->
                Some (Search.ReferenceParam ("status", value))
            | FhirResourceType.ImagingStudy, "started" ->
                let prefix, dateStr = Search.parseDatePrefix value
                match DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None) with
                | true, dt -> Some (Search.DateParam ("started", prefix, dt))
                | _ -> None

            // Organization
            | FhirResourceType.Organization, "name" ->
                Some (Search.StringParam ("name", value))

            // Practitioner
            | FhirResourceType.Practitioner, "name" ->
                Some (Search.OrStringParam (["family_name"; "given_name"], value))

            | _ -> None
        )
