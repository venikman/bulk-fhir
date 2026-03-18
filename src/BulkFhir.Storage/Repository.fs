namespace BulkFhir.Storage

open System
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
                use conn = new NpgsqlConnection(connString)
                let! results = conn.QueryAsync<string>(sql, {| Ids = ids |> Array.ofList |})
                return results |> Seq.toList
        }

    /// Read a single resource by ID, returning the raw JSON.
    let readById (connString: string) (resourceType: FhirResourceType) (id: string) =
        task {
            let table = FhirResourceType.tableName resourceType
            let sql = $"SELECT resource_text FROM {table} WHERE id = @Id LIMIT 1"
            use conn = new NpgsqlConnection(connString)
            let! result = conn.QuerySingleOrDefaultAsync<string>(sql, {| Id = id |})
            return if isNull result then None else Some result
        }

    /// Search resources returning raw JSON. Applies WHERE clause from search params.
    let search (connString: string) (resourceType: FhirResourceType) (searchParams: Search.SearchParam list) =
        task {
            let table = FhirResourceType.tableName resourceType
            let whereClause, parameters = Search.buildWhere searchParams
            let sql = $"SELECT resource_text FROM {table}{whereClause} LIMIT 100"

            use conn = new NpgsqlConnection(connString)
            let dynParams = DynamicParameters()
            for (name, value) in parameters do
                dynParams.Add(name, value)

            let! results = conn.QueryAsync<string>(sql, dynParams)
            return results |> Seq.toList
        }

    /// List all resources of a type (no filter).
    let listAll (connString: string) (resourceType: FhirResourceType) =
        search connString resourceType []

    /// Search groups by identifier (system|value).
    let searchGroupsByIdentifier (connString: string) (system: string) (value: string) =
        task {
            let sql = "SELECT resource_text FROM groups WHERE identifier_system = @System AND identifier_value = @Value"
            use conn = new NpgsqlConnection(connString)
            let! results = conn.QueryAsync<string>(sql, {| System = system; Value = value |})
            return results |> Seq.toList
        }

    /// Search groups by name (case-insensitive partial match).
    let searchGroupsByName (connString: string) (name: string) =
        task {
            let sql = "SELECT resource_text FROM groups WHERE lower(name) LIKE @Name"
            use conn = new NpgsqlConnection(connString)
            let! results = conn.QueryAsync<string>(sql, {| Name = $"%%{name.ToLowerInvariant()}%%" |})
            return results |> Seq.toList
        }

    /// Get all resource_text for a given type and list of subject/patient references.
    /// Used by bulk export to resolve references from a Group.
    let getBySubjectRefs (connString: string) (resourceType: FhirResourceType) (refs: string list) =
        task {
            if refs.IsEmpty then return []
            else
                let table = FhirResourceType.tableName resourceType
                // Determine the reference column name based on the table
                let refCol =
                    match resourceType with
                    | FhirResourceType.AllergyIntolerance
                    | FhirResourceType.Immunization
                    | FhirResourceType.Claim
                    | FhirResourceType.ExplanationOfBenefit
                    | FhirResourceType.Device -> "patient_ref"
                    | _ -> "subject_ref"

                let sql = $"SELECT resource_text FROM {table} WHERE {refCol} = ANY(@Refs)"
                use conn = new NpgsqlConnection(connString)
                let! results = conn.QueryAsync<string>(sql, {| Refs = refs |> Array.ofList |})
                return results |> Seq.toList
        }

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
                match DateTime.TryParse(dateStr) with
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
                match DateTime.TryParse(dateStr) with
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
                match DateTime.TryParse(dateStr) with
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

            | _ -> None
        )
