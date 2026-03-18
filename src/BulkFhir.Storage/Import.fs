namespace BulkFhir.Storage

open System
open System.IO
open System.Text.Json
open Npgsql
open BulkFhir.Domain

/// Imports NDJSON files into PostgreSQL.
/// Parses each line, extracts searchable columns, and bulk-inserts.
module Import =

    let private tryGetString (elem: JsonElement) (prop: string) =
        match elem.TryGetProperty(prop) with
        | true, v when v.ValueKind = JsonValueKind.String -> Some (v.GetString())
        | _ -> None

    /// Extract system + code from a CodeableConcept or CodeableConcept[] property.
    /// Handles both `"code": { "coding": [...] }` and `"category": [{ "coding": [...] }]`.
    let private tryGetFirstCoding (elem: JsonElement) (prop: string) =
        match elem.TryGetProperty(prop) with
        | true, v ->
            // If it's an array of CodeableConcepts, take the first element
            let cc =
                if v.ValueKind = JsonValueKind.Array && v.GetArrayLength() > 0 then v.[0]
                else v
            match cc.TryGetProperty("coding") with
            | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
                let first = arr.[0]
                let system = tryGetString first "system"
                let code = tryGetString first "code"
                system, code
            | _ -> None, None
        | _ -> None, None

    let private tryGetFirstCodingCode (elem: JsonElement) (prop: string) =
        let _, code = tryGetFirstCoding elem prop
        code

    let private tryGetNestedRef (elem: JsonElement) (prop: string) =
        match elem.TryGetProperty(prop) with
        | true, v -> tryGetString v "reference"
        | _ -> None

    let private tryParseTimestamp (s: string option) =
        match s with
        | Some v ->
            match DateTime.TryParse(v) with
            | true, dt -> box dt :> obj
            | _ -> DBNull.Value :> obj
        | None -> DBNull.Value :> obj

    let private toDbNull (s: string option) : obj =
        match s with
        | Some v -> v :> obj
        | None -> DBNull.Value :> obj

    let private importLine (conn: NpgsqlConnection) (resourceType: string) (line: string) =
        task {
            use doc = JsonDocument.Parse(line)
            let root = doc.RootElement
            let id = root.GetProperty("id").GetString()

            match resourceType with
            | "Patient" ->
                let familyName =
                    match root.TryGetProperty("name") with
                    | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
                        tryGetString arr.[0] "family"
                    | _ -> None
                let givenName =
                    match root.TryGetProperty("name") with
                    | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
                        match arr.[0].TryGetProperty("given") with
                        | true, g when g.ValueKind = JsonValueKind.Array && g.GetArrayLength() > 0 ->
                            Some (g.[0].GetString())
                        | _ -> None
                    | _ -> None
                let sql = "INSERT INTO patients (id, family_name, given_name, birth_date, gender, resource_text) VALUES (@id, @fn, @gn, @bd, @g, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("fn", toDbNull familyName) |> ignore
                cmd.Parameters.AddWithValue("gn", toDbNull givenName) |> ignore
                cmd.Parameters.AddWithValue("bd", tryParseTimestamp (tryGetString root "birthDate")) |> ignore
                cmd.Parameters.AddWithValue("g", toDbNull (tryGetString root "gender")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Encounter" ->
                let subjectRef = tryGetNestedRef root "subject"
                let practRef =
                    match root.TryGetProperty("participant") with
                    | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
                        tryGetNestedRef arr.[0] "individual"
                    | _ -> None
                let periodStart =
                    match root.TryGetProperty("period") with
                    | true, p -> tryGetString p "start"
                    | _ -> None
                let periodEnd =
                    match root.TryGetProperty("period") with
                    | true, p -> tryGetString p "end"
                    | _ -> None
                let typeSys, typeCode = tryGetFirstCoding root "type"
                let reasonSys, reasonCode = tryGetFirstCoding root "reasonCode"
                let sql = "INSERT INTO encounters (id, subject_ref, status, period_start, period_end, type_system, type_code, practitioner_ref, reason_system, reason_code, resource_text) VALUES (@id, @sr, @st, @ps, @pe, @ts, @tc, @pr, @rs, @rc, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull subjectRef) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("ps", tryParseTimestamp periodStart) |> ignore
                cmd.Parameters.AddWithValue("pe", tryParseTimestamp periodEnd) |> ignore
                cmd.Parameters.AddWithValue("ts", toDbNull typeSys) |> ignore
                cmd.Parameters.AddWithValue("tc", toDbNull typeCode) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull practRef) |> ignore
                cmd.Parameters.AddWithValue("rs", toDbNull reasonSys) |> ignore
                cmd.Parameters.AddWithValue("rc", toDbNull reasonCode) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Condition" ->
                let codeSys, codeVal = tryGetFirstCoding root "code"
                let clinicalStatus = tryGetFirstCodingCode root "clinicalStatus"
                let sql = "INSERT INTO conditions (id, subject_ref, code_system, code_value, clinical_status, category_code, onset_date, resource_text) VALUES (@id, @sr, @cs, @cv, @cls, @cat, @od, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("cls", toDbNull clinicalStatus) |> ignore
                cmd.Parameters.AddWithValue("cat", toDbNull (tryGetFirstCodingCode root "category")) |> ignore
                cmd.Parameters.AddWithValue("od", tryParseTimestamp (tryGetString root "onsetDateTime")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Observation" ->
                let codeSys, codeVal = tryGetFirstCoding root "code"
                let sql = "INSERT INTO observations (id, subject_ref, code_system, code_value, category_code, effective_date, status, resource_text) VALUES (@id, @sr, @cs, @cv, @cat, @ed, @st, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("cat", toDbNull (tryGetFirstCodingCode root "category")) |> ignore
                cmd.Parameters.AddWithValue("ed", tryParseTimestamp (tryGetString root "effectiveDateTime")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Procedure" ->
                let codeSys, codeVal = tryGetFirstCoding root "code"
                let sql = "INSERT INTO procedures (id, subject_ref, code_system, code_value, status, resource_text) VALUES (@id, @sr, @cs, @cv, @st, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "MedicationRequest" ->
                let codeSys, codeVal = tryGetFirstCoding root "medicationCodeableConcept"
                let sql = "INSERT INTO medication_requests (id, subject_ref, code_system, code_value, status, resource_text) VALUES (@id, @sr, @cs, @cv, @st, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "AllergyIntolerance" ->
                let codeSys, codeVal = tryGetFirstCoding root "code"
                let sql = "INSERT INTO allergy_intolerances (id, patient_ref, code_system, code_value, clinical_status, resource_text) VALUES (@id, @pr, @cs, @cv, @cls, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("cls", toDbNull (tryGetFirstCodingCode root "clinicalStatus")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Immunization" ->
                let sql = "INSERT INTO immunizations (id, patient_ref, status, occurrence_date, resource_text) VALUES (@id, @pr, @st, @od, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("od", tryParseTimestamp (tryGetString root "occurrenceDateTime")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Group" ->
                let idSys, idVal =
                    match root.TryGetProperty("identifier") with
                    | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
                        tryGetString arr.[0] "system", tryGetString arr.[0] "value"
                    | _ -> None, None
                let sql = "INSERT INTO groups (id, name, identifier_system, identifier_value, resource_text) VALUES (@id, @n, @is, @iv, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("n", toDbNull (tryGetString root "name")) |> ignore
                cmd.Parameters.AddWithValue("is", toDbNull idSys) |> ignore
                cmd.Parameters.AddWithValue("iv", toDbNull idVal) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Organization" ->
                let sql = "INSERT INTO organizations (id, name, resource_text) VALUES (@id, @n, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("n", toDbNull (tryGetString root "name")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Practitioner" ->
                let familyName =
                    match root.TryGetProperty("name") with
                    | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
                        tryGetString arr.[0] "family"
                    | _ -> None
                let givenName =
                    match root.TryGetProperty("name") with
                    | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
                        match arr.[0].TryGetProperty("given") with
                        | true, g when g.ValueKind = JsonValueKind.Array && g.GetArrayLength() > 0 ->
                            Some (g.[0].GetString())
                        | _ -> None
                    | _ -> None
                let sql = "INSERT INTO practitioners (id, family_name, given_name, resource_text) VALUES (@id, @fn, @gn, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("fn", toDbNull familyName) |> ignore
                cmd.Parameters.AddWithValue("gn", toDbNull givenName) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "CarePlan" ->
                let sql = "INSERT INTO care_plans (id, subject_ref, status, resource_text) VALUES (@id, @sr, @st, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "CareTeam" ->
                let sql = "INSERT INTO care_teams (id, subject_ref, status, resource_text) VALUES (@id, @sr, @st, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Claim" ->
                let sql = "INSERT INTO claims (id, patient_ref, status, created, resource_text) VALUES (@id, @pr, @st, @cr, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("cr", tryParseTimestamp (tryGetString root "created")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "ExplanationOfBenefit" ->
                let sql = "INSERT INTO explanation_of_benefits (id, patient_ref, status, created, resource_text) VALUES (@id, @pr, @st, @cr, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("cr", tryParseTimestamp (tryGetString root "created")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "DiagnosticReport" ->
                let codeSys, codeVal = tryGetFirstCoding root "code"
                let sql = "INSERT INTO diagnostic_reports (id, subject_ref, code_system, code_value, effective_date, status, resource_text) VALUES (@id, @sr, @cs, @cv, @ed, @st, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("ed", tryParseTimestamp (tryGetString root "effectiveDateTime")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "DocumentReference" ->
                let sql = "INSERT INTO document_references (id, subject_ref, status, date, resource_text) VALUES (@id, @sr, @st, @dt, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("dt", tryParseTimestamp (tryGetString root "date")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "Device" ->
                let sql = "INSERT INTO devices (id, patient_ref, status, resource_text) VALUES (@id, @pr, @st, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | "ImagingStudy" ->
                let sql = "INSERT INTO imaging_studies (id, subject_ref, status, started, resource_text) VALUES (@id, @sr, @st, @s, @rt) ON CONFLICT (id) DO NOTHING"
                use cmd = new NpgsqlCommand(sql, conn)
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("s", tryParseTimestamp (tryGetString root "started")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore
                let! _ = cmd.ExecuteNonQueryAsync()
                ()

            | _ ->
                printfn $"  Skipping unknown resource type: {resourceType}"
        }

    /// Import a single NDJSON file into the database.
    let importFile (connString: string) (filePath: string) =
        task {
            let fileName = Path.GetFileName(filePath)
            // Parse resource type from filename like "116.Patient.ndjson"
            let parts = fileName.Split('.')
            if parts.Length >= 2 then
                let resourceType = parts.[1]
                printfn $"Importing {fileName} ({resourceType})..."
                use conn = new NpgsqlConnection(connString)
                do! conn.OpenAsync()
                let mutable count = 0
                for line in File.ReadLines(filePath) do
                    if not (String.IsNullOrWhiteSpace(line)) then
                        do! importLine conn resourceType line
                        count <- count + 1
                printfn $"  Imported {count} {resourceType} resources"
        }

    /// Import all NDJSON files from a directory.
    let importDirectory (connString: string) (dirPath: string) =
        task {
            let files = Directory.GetFiles(dirPath, "*.ndjson") |> Array.sort
            printfn $"Found {files.Length} NDJSON files in {dirPath}"
            for file in files do
                do! importFile connString file
            printfn "Import complete."
        }
