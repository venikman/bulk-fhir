#r "nuget: Npgsql"

open System
open System.IO
open System.Text.Json
open Npgsql

let connString =
    Environment.GetEnvironmentVariable("CONNECTION_STRING")
    |> Option.ofObj
    |> Option.defaultValue "Host=localhost;Port=5433;Database=bulk_fhir;Username=postgres;Password=postgres"

let dataDir =
    match fsi.CommandLineArgs |> Array.tryItem 1 with
    | Some d -> d
    | None ->
        Environment.GetEnvironmentVariable("DATA_DIR")
        |> Option.ofObj
        |> Option.defaultValue "./downloads"

// ─── Helpers ──────────────────────────────────────

let tryGetString (elem: JsonElement) (prop: string) =
    match elem.TryGetProperty(prop) with
    | true, v when v.ValueKind = JsonValueKind.String -> Some (v.GetString())
    | _ -> None

let tryGetFirstCoding (elem: JsonElement) (prop: string) =
    match elem.TryGetProperty(prop) with
    | true, v ->
        let cc =
            if v.ValueKind = JsonValueKind.Array && v.GetArrayLength() > 0 then v.[0]
            else v
        match cc.TryGetProperty("coding") with
        | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
            let first = arr.[0]
            tryGetString first "system", tryGetString first "code"
        | _ -> None, None
    | _ -> None, None

let tryGetFirstCodingCode (elem: JsonElement) (prop: string) =
    let _, code = tryGetFirstCoding elem prop
    code

let tryGetNestedRef (elem: JsonElement) (prop: string) =
    match elem.TryGetProperty(prop) with
    | true, v -> tryGetString v "reference"
    | _ -> None

let tryGetHumanName (elem: JsonElement) =
    match elem.TryGetProperty("name") with
    | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
        let first = arr.[0]
        let family = tryGetString first "family"
        let given =
            match first.TryGetProperty("given") with
            | true, g when g.ValueKind = JsonValueKind.Array && g.GetArrayLength() > 0 ->
                Some (g.[0].GetString())
            | _ -> None
        family, given
    | _ -> None, None

let tryParseTimestamp (s: string option) =
    match s with
    | Some v ->
        match DateTime.TryParse(v) with
        | true, dt -> box dt :> obj
        | _ -> DBNull.Value :> obj
    | None -> DBNull.Value :> obj

let toDbNull (s: string option) : obj =
    match s with
    | Some v -> v :> obj
    | None -> DBNull.Value :> obj

// ─── Schema ───────────────────────────────────────

let ddl = """
CREATE TABLE IF NOT EXISTS patients (
    id TEXT PRIMARY KEY, family_name TEXT, given_name TEXT,
    birth_date DATE, gender TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_patients_name ON patients (lower(family_name), lower(given_name));
CREATE INDEX IF NOT EXISTS idx_patients_birth ON patients (birth_date);
CREATE INDEX IF NOT EXISTS idx_patients_gender ON patients (gender);

CREATE TABLE IF NOT EXISTS encounters (
    id TEXT PRIMARY KEY, subject_ref TEXT, status TEXT,
    period_start TIMESTAMP, period_end TIMESTAMP,
    type_system TEXT, type_code TEXT, practitioner_ref TEXT,
    reason_system TEXT, reason_code TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_encounters_subject ON encounters (subject_ref);
CREATE INDEX IF NOT EXISTS idx_encounters_date ON encounters (period_start);
CREATE INDEX IF NOT EXISTS idx_encounters_status ON encounters (status);

CREATE TABLE IF NOT EXISTS conditions (
    id TEXT PRIMARY KEY, subject_ref TEXT, code_system TEXT, code_value TEXT,
    clinical_status TEXT, category_code TEXT, onset_date TIMESTAMP, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_conditions_subject ON conditions (subject_ref);
CREATE INDEX IF NOT EXISTS idx_conditions_code ON conditions (code_system, code_value);
CREATE INDEX IF NOT EXISTS idx_conditions_clinical ON conditions (clinical_status);

CREATE TABLE IF NOT EXISTS observations (
    id TEXT PRIMARY KEY, subject_ref TEXT, code_system TEXT, code_value TEXT,
    category_code TEXT, effective_date TIMESTAMP, status TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_observations_subject ON observations (subject_ref);
CREATE INDEX IF NOT EXISTS idx_observations_code ON observations (code_system, code_value);
CREATE INDEX IF NOT EXISTS idx_observations_date ON observations (effective_date);

CREATE TABLE IF NOT EXISTS procedures (
    id TEXT PRIMARY KEY, subject_ref TEXT, code_system TEXT, code_value TEXT,
    status TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_procedures_subject ON procedures (subject_ref);
CREATE INDEX IF NOT EXISTS idx_procedures_code ON procedures (code_system, code_value);

CREATE TABLE IF NOT EXISTS medication_requests (
    id TEXT PRIMARY KEY, subject_ref TEXT, code_system TEXT, code_value TEXT,
    status TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_medrequests_subject ON medication_requests (subject_ref);
CREATE INDEX IF NOT EXISTS idx_medrequests_code ON medication_requests (code_system, code_value);
CREATE INDEX IF NOT EXISTS idx_medrequests_status ON medication_requests (status);

CREATE TABLE IF NOT EXISTS allergy_intolerances (
    id TEXT PRIMARY KEY, patient_ref TEXT, code_system TEXT, code_value TEXT,
    clinical_status TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_allergies_patient ON allergy_intolerances (patient_ref);

CREATE TABLE IF NOT EXISTS immunizations (
    id TEXT PRIMARY KEY, patient_ref TEXT, status TEXT,
    occurrence_date TIMESTAMP, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_immunizations_patient ON immunizations (patient_ref);

CREATE TABLE IF NOT EXISTS groups (
    id TEXT PRIMARY KEY, name TEXT, identifier_system TEXT,
    identifier_value TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_groups_name ON groups (lower(name));

CREATE TABLE IF NOT EXISTS organizations (
    id TEXT PRIMARY KEY, name TEXT, resource_text TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS practitioners (
    id TEXT PRIMARY KEY, family_name TEXT, given_name TEXT, resource_text TEXT NOT NULL);

CREATE TABLE IF NOT EXISTS care_plans (
    id TEXT PRIMARY KEY, subject_ref TEXT, status TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_careplans_subject ON care_plans (subject_ref);

CREATE TABLE IF NOT EXISTS care_teams (
    id TEXT PRIMARY KEY, subject_ref TEXT, status TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_careteams_subject ON care_teams (subject_ref);

CREATE TABLE IF NOT EXISTS claims (
    id TEXT PRIMARY KEY, patient_ref TEXT, status TEXT,
    created TIMESTAMP, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_claims_patient ON claims (patient_ref);

CREATE TABLE IF NOT EXISTS explanation_of_benefits (
    id TEXT PRIMARY KEY, patient_ref TEXT, status TEXT,
    created TIMESTAMP, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_eob_patient ON explanation_of_benefits (patient_ref);

CREATE TABLE IF NOT EXISTS diagnostic_reports (
    id TEXT PRIMARY KEY, subject_ref TEXT, code_system TEXT, code_value TEXT,
    effective_date TIMESTAMP, status TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_diagreports_subject ON diagnostic_reports (subject_ref);

CREATE TABLE IF NOT EXISTS document_references (
    id TEXT PRIMARY KEY, subject_ref TEXT, status TEXT,
    date TIMESTAMP, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_docrefs_subject ON document_references (subject_ref);

CREATE TABLE IF NOT EXISTS devices (
    id TEXT PRIMARY KEY, patient_ref TEXT, status TEXT, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_devices_patient ON devices (patient_ref);

CREATE TABLE IF NOT EXISTS imaging_studies (
    id TEXT PRIMARY KEY, subject_ref TEXT, status TEXT,
    started TIMESTAMP, resource_text TEXT NOT NULL);
CREATE INDEX IF NOT EXISTS idx_imaging_subject ON imaging_studies (subject_ref);
"""

// ─── Import ───────────────────────────────────────

let importLine (conn: NpgsqlConnection) (resourceType: string) (line: string) =
    use doc = JsonDocument.Parse(line)
    let root = doc.RootElement
    let id = root.GetProperty("id").GetString()

    let exec (sql: string) (addParams: NpgsqlCommand -> unit) =
        use cmd = new NpgsqlCommand(sql, conn)
        addParams cmd
        cmd.ExecuteNonQuery() |> ignore

    match resourceType with
    | "Patient" ->
        let fn, gn = tryGetHumanName root
        exec "INSERT INTO patients (id, family_name, given_name, birth_date, gender, resource_text) VALUES (@id, @fn, @gn, @bd, @g, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("fn", toDbNull fn) |> ignore
                cmd.Parameters.AddWithValue("gn", toDbNull gn) |> ignore
                cmd.Parameters.AddWithValue("bd", tryParseTimestamp (tryGetString root "birthDate")) |> ignore
                cmd.Parameters.AddWithValue("g", toDbNull (tryGetString root "gender")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Encounter" ->
        let subjectRef = tryGetNestedRef root "subject"
        let practRef =
            match root.TryGetProperty("participant") with
            | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
                tryGetNestedRef arr.[0] "individual"
            | _ -> None
        let periodStart, periodEnd =
            match root.TryGetProperty("period") with
            | true, p -> tryGetString p "start", tryGetString p "end"
            | _ -> None, None
        let typeSys, typeCode = tryGetFirstCoding root "type"
        let reasonSys, reasonCode = tryGetFirstCoding root "reasonCode"
        exec "INSERT INTO encounters (id, subject_ref, status, period_start, period_end, type_system, type_code, practitioner_ref, reason_system, reason_code, resource_text) VALUES (@id, @sr, @st, @ps, @pe, @ts, @tc, @pr, @rs, @rc, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
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
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Condition" ->
        let codeSys, codeVal = tryGetFirstCoding root "code"
        exec "INSERT INTO conditions (id, subject_ref, code_system, code_value, clinical_status, category_code, onset_date, resource_text) VALUES (@id, @sr, @cs, @cv, @cls, @cat, @od, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("cls", toDbNull (tryGetFirstCodingCode root "clinicalStatus")) |> ignore
                cmd.Parameters.AddWithValue("cat", toDbNull (tryGetFirstCodingCode root "category")) |> ignore
                cmd.Parameters.AddWithValue("od", tryParseTimestamp (tryGetString root "onsetDateTime")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Observation" ->
        let codeSys, codeVal = tryGetFirstCoding root "code"
        exec "INSERT INTO observations (id, subject_ref, code_system, code_value, category_code, effective_date, status, resource_text) VALUES (@id, @sr, @cs, @cv, @cat, @ed, @st, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("cat", toDbNull (tryGetFirstCodingCode root "category")) |> ignore
                cmd.Parameters.AddWithValue("ed", tryParseTimestamp (tryGetString root "effectiveDateTime")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Procedure" ->
        let codeSys, codeVal = tryGetFirstCoding root "code"
        exec "INSERT INTO procedures (id, subject_ref, code_system, code_value, status, resource_text) VALUES (@id, @sr, @cs, @cv, @st, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "MedicationRequest" ->
        let codeSys, codeVal = tryGetFirstCoding root "medicationCodeableConcept"
        exec "INSERT INTO medication_requests (id, subject_ref, code_system, code_value, status, resource_text) VALUES (@id, @sr, @cs, @cv, @st, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "AllergyIntolerance" ->
        let codeSys, codeVal = tryGetFirstCoding root "code"
        exec "INSERT INTO allergy_intolerances (id, patient_ref, code_system, code_value, clinical_status, resource_text) VALUES (@id, @pr, @cs, @cv, @cls, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("cls", toDbNull (tryGetFirstCodingCode root "clinicalStatus")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Immunization" ->
        exec "INSERT INTO immunizations (id, patient_ref, status, occurrence_date, resource_text) VALUES (@id, @pr, @st, @od, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("od", tryParseTimestamp (tryGetString root "occurrenceDateTime")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Group" ->
        let idSys, idVal =
            match root.TryGetProperty("identifier") with
            | true, arr when arr.ValueKind = JsonValueKind.Array && arr.GetArrayLength() > 0 ->
                tryGetString arr.[0] "system", tryGetString arr.[0] "value"
            | _ -> None, None
        exec "INSERT INTO groups (id, name, identifier_system, identifier_value, resource_text) VALUES (@id, @n, @is, @iv, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("n", toDbNull (tryGetString root "name")) |> ignore
                cmd.Parameters.AddWithValue("is", toDbNull idSys) |> ignore
                cmd.Parameters.AddWithValue("iv", toDbNull idVal) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Organization" ->
        exec "INSERT INTO organizations (id, name, resource_text) VALUES (@id, @n, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("n", toDbNull (tryGetString root "name")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Practitioner" ->
        let fn, gn = tryGetHumanName root
        exec "INSERT INTO practitioners (id, family_name, given_name, resource_text) VALUES (@id, @fn, @gn, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("fn", toDbNull fn) |> ignore
                cmd.Parameters.AddWithValue("gn", toDbNull gn) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "CarePlan" ->
        exec "INSERT INTO care_plans (id, subject_ref, status, resource_text) VALUES (@id, @sr, @st, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "CareTeam" ->
        exec "INSERT INTO care_teams (id, subject_ref, status, resource_text) VALUES (@id, @sr, @st, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Claim" ->
        exec "INSERT INTO claims (id, patient_ref, status, created, resource_text) VALUES (@id, @pr, @st, @cr, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("cr", tryParseTimestamp (tryGetString root "created")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "ExplanationOfBenefit" ->
        exec "INSERT INTO explanation_of_benefits (id, patient_ref, status, created, resource_text) VALUES (@id, @pr, @st, @cr, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("cr", tryParseTimestamp (tryGetString root "created")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "DiagnosticReport" ->
        let codeSys, codeVal = tryGetFirstCoding root "code"
        exec "INSERT INTO diagnostic_reports (id, subject_ref, code_system, code_value, effective_date, status, resource_text) VALUES (@id, @sr, @cs, @cv, @ed, @st, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("cs", toDbNull codeSys) |> ignore
                cmd.Parameters.AddWithValue("cv", toDbNull codeVal) |> ignore
                cmd.Parameters.AddWithValue("ed", tryParseTimestamp (tryGetString root "effectiveDateTime")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "DocumentReference" ->
        exec "INSERT INTO document_references (id, subject_ref, status, date, resource_text) VALUES (@id, @sr, @st, @dt, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("dt", tryParseTimestamp (tryGetString root "date")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "Device" ->
        exec "INSERT INTO devices (id, patient_ref, status, resource_text) VALUES (@id, @pr, @st, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("pr", toDbNull (tryGetNestedRef root "patient")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | "ImagingStudy" ->
        exec "INSERT INTO imaging_studies (id, subject_ref, status, started, resource_text) VALUES (@id, @sr, @st, @s, @rt) ON CONFLICT (id) DO NOTHING"
            (fun cmd ->
                cmd.Parameters.AddWithValue("id", id) |> ignore
                cmd.Parameters.AddWithValue("sr", toDbNull (tryGetNestedRef root "subject")) |> ignore
                cmd.Parameters.AddWithValue("st", toDbNull (tryGetString root "status")) |> ignore
                cmd.Parameters.AddWithValue("s", tryParseTimestamp (tryGetString root "started")) |> ignore
                cmd.Parameters.AddWithValue("rt", line) |> ignore)

    | _ ->
        printfn $"  Skipping unknown resource type: {resourceType}"

// ─── Main ─────────────────────────────────────────

printfn "BulkFhir Import"
printfn $"  Connection: {connString.[..min 30 (connString.Length - 1)]}..."
printfn $"  Data dir:   {dataDir}"
printfn ""

// Wait for DB
printfn "Waiting for database..."
let mutable ready = false
let mutable attempts = 0
while not ready && attempts < 15 do
    try
        use conn = new NpgsqlConnection(connString)
        conn.Open()
        conn.Close()
        ready <- true
    with _ ->
        attempts <- attempts + 1
        printfn $"  Attempt {attempts}/15 - retrying in 2s..."
        Threading.Thread.Sleep(2000)
if not ready then
    eprintfn "Error: Could not connect to database after 30 seconds."
    exit 1
printfn "Database is ready."
printfn ""

// Create schema
printfn "Creating schema..."
use conn = new NpgsqlConnection(connString)
conn.Open()
use cmd = new NpgsqlCommand(ddl, conn)
cmd.ExecuteNonQuery() |> ignore
printfn "Schema created."
printfn ""

// Import NDJSON files
let files = Directory.GetFiles(dataDir, "*.ndjson") |> Array.sort
printfn $"Found {files.Length} NDJSON files in {dataDir}"
for file in files do
    let fileName = Path.GetFileName(file)
    let parts = fileName.Split('.')
    if parts.Length >= 2 then
        let resourceType = parts.[1]
        printfn $"Importing {fileName} ({resourceType})..."
        use tx = conn.BeginTransaction()
        let mutable count = 0
        for line in File.ReadLines(file) do
            if not (String.IsNullOrWhiteSpace(line)) then
                importLine conn resourceType line
                count <- count + 1
        tx.Commit()
        printfn $"  Imported {count} {resourceType} resources"

printfn ""
printfn "Import complete."
