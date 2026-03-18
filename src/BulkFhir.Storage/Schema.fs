namespace BulkFhir.Storage

open Npgsql

/// DDL for all FHIR resource tables.
/// Each table has typed searchable columns + resource_text TEXT for raw JSON.
module Schema =

    let ddl = """
-- Patients: searchable by name, birthdate, gender, general-practitioner
CREATE TABLE IF NOT EXISTS patients (
    id              TEXT PRIMARY KEY,
    family_name     TEXT,
    given_name      TEXT,
    birth_date      DATE,
    gender          TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_patients_name ON patients (lower(family_name), lower(given_name));
CREATE INDEX IF NOT EXISTS idx_patients_birth ON patients (birth_date);
CREATE INDEX IF NOT EXISTS idx_patients_gender ON patients (gender);

-- Encounters: searchable by patient, date, status, type, practitioner, reason-code
CREATE TABLE IF NOT EXISTS encounters (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    status          TEXT,
    period_start    TIMESTAMP,
    period_end      TIMESTAMP,
    type_system     TEXT,
    type_code       TEXT,
    practitioner_ref TEXT,
    reason_system   TEXT,
    reason_code     TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_encounters_subject ON encounters (subject_ref);
CREATE INDEX IF NOT EXISTS idx_encounters_date ON encounters (period_start);
CREATE INDEX IF NOT EXISTS idx_encounters_status ON encounters (status);

-- Conditions: searchable by patient, code, clinical-status, category
CREATE TABLE IF NOT EXISTS conditions (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    code_system     TEXT,
    code_value      TEXT,
    clinical_status TEXT,
    category_code   TEXT,
    onset_date      TIMESTAMP,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_conditions_subject ON conditions (subject_ref);
CREATE INDEX IF NOT EXISTS idx_conditions_code ON conditions (code_system, code_value);
CREATE INDEX IF NOT EXISTS idx_conditions_clinical ON conditions (clinical_status);

-- Observations: searchable by patient, code, category, date
CREATE TABLE IF NOT EXISTS observations (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    code_system     TEXT,
    code_value      TEXT,
    category_code   TEXT,
    effective_date  TIMESTAMP,
    status          TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_observations_subject ON observations (subject_ref);
CREATE INDEX IF NOT EXISTS idx_observations_code ON observations (code_system, code_value);
CREATE INDEX IF NOT EXISTS idx_observations_date ON observations (effective_date);

-- Procedures: searchable by patient, code
CREATE TABLE IF NOT EXISTS procedures (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    code_system     TEXT,
    code_value      TEXT,
    status          TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_procedures_subject ON procedures (subject_ref);
CREATE INDEX IF NOT EXISTS idx_procedures_code ON procedures (code_system, code_value);

-- MedicationRequests: searchable by patient, code, status
CREATE TABLE IF NOT EXISTS medication_requests (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    code_system     TEXT,
    code_value      TEXT,
    status          TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_medrequests_subject ON medication_requests (subject_ref);
CREATE INDEX IF NOT EXISTS idx_medrequests_code ON medication_requests (code_system, code_value);
CREATE INDEX IF NOT EXISTS idx_medrequests_status ON medication_requests (status);

-- AllergyIntolerances: searchable by patient
CREATE TABLE IF NOT EXISTS allergy_intolerances (
    id              TEXT PRIMARY KEY,
    patient_ref     TEXT,
    code_system     TEXT,
    code_value      TEXT,
    clinical_status TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_allergies_patient ON allergy_intolerances (patient_ref);

-- Immunizations
CREATE TABLE IF NOT EXISTS immunizations (
    id              TEXT PRIMARY KEY,
    patient_ref     TEXT,
    status          TEXT,
    occurrence_date TIMESTAMP,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_immunizations_patient ON immunizations (patient_ref);

-- Groups: searchable by identifier, name
CREATE TABLE IF NOT EXISTS groups (
    id              TEXT PRIMARY KEY,
    name            TEXT,
    identifier_system TEXT,
    identifier_value  TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_groups_name ON groups (lower(name));

-- Organizations
CREATE TABLE IF NOT EXISTS organizations (
    id              TEXT PRIMARY KEY,
    name            TEXT,
    resource_text   TEXT NOT NULL
);

-- Practitioners
CREATE TABLE IF NOT EXISTS practitioners (
    id              TEXT PRIMARY KEY,
    family_name     TEXT,
    given_name      TEXT,
    resource_text   TEXT NOT NULL
);

-- CarePlans
CREATE TABLE IF NOT EXISTS care_plans (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    status          TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_careplans_subject ON care_plans (subject_ref);

-- CareTeams
CREATE TABLE IF NOT EXISTS care_teams (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    status          TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_careteams_subject ON care_teams (subject_ref);

-- Claims
CREATE TABLE IF NOT EXISTS claims (
    id              TEXT PRIMARY KEY,
    patient_ref     TEXT,
    status          TEXT,
    created         TIMESTAMP,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_claims_patient ON claims (patient_ref);

-- ExplanationOfBenefits
CREATE TABLE IF NOT EXISTS explanation_of_benefits (
    id              TEXT PRIMARY KEY,
    patient_ref     TEXT,
    status          TEXT,
    created         TIMESTAMP,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_eob_patient ON explanation_of_benefits (patient_ref);

-- DiagnosticReports
CREATE TABLE IF NOT EXISTS diagnostic_reports (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    code_system     TEXT,
    code_value      TEXT,
    effective_date  TIMESTAMP,
    status          TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_diagreports_subject ON diagnostic_reports (subject_ref);

-- DocumentReferences
CREATE TABLE IF NOT EXISTS document_references (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    status          TEXT,
    date            TIMESTAMP,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_docrefs_subject ON document_references (subject_ref);

-- Devices
CREATE TABLE IF NOT EXISTS devices (
    id              TEXT PRIMARY KEY,
    patient_ref     TEXT,
    status          TEXT,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_devices_patient ON devices (patient_ref);

-- ImagingStudies
CREATE TABLE IF NOT EXISTS imaging_studies (
    id              TEXT PRIMARY KEY,
    subject_ref     TEXT,
    status          TEXT,
    started         TIMESTAMP,
    resource_text   TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_imaging_subject ON imaging_studies (subject_ref);
"""

// Dashboard views and bulk_export_jobs table removed — dashboard API was dead code.
// Export jobs are tracked in-memory only (ConcurrentDictionary in BulkExport.fs).

    let createSchema (connString: string) =
        task {
            use conn = Connection.createConnection connString
            do! conn.OpenAsync()
            use cmd = new NpgsqlCommand(ddl, conn)
            let! _ = cmd.ExecuteNonQueryAsync()
            return ()
        }
