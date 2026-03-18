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

-- Bulk export jobs surfaced in the dashboard
CREATE TABLE IF NOT EXISTS bulk_export_jobs (
    id              TEXT PRIMARY KEY,
    group_id        TEXT NOT NULL,
    status          TEXT NOT NULL DEFAULT 'pending',
    request_url     TEXT NOT NULL,
    types           TEXT NOT NULL,
    created_at      TIMESTAMP NOT NULL DEFAULT now(),
    completed_at    TIMESTAMP,
    expires_at      TIMESTAMP,
    progress        TEXT
);

CREATE OR REPLACE VIEW dashboard_resource_coverage_v AS
SELECT
    'Patient'::text AS resource_type,
    count(*)::int AS resource_count,
    0::int AS subject_linked_count,
    NULL::timestamp AS first_clinical_date,
    NULL::timestamp AS latest_clinical_date,
    '{}'::jsonb AS status_summary
FROM patients
UNION ALL
SELECT
    'Practitioner'::text,
    count(*)::int,
    0::int,
    NULL::timestamp,
    NULL::timestamp,
    '{}'::jsonb
FROM practitioners
UNION ALL
SELECT
    'Organization'::text,
    count(*)::int,
    0::int,
    NULL::timestamp,
    NULL::timestamp,
    '{}'::jsonb
FROM organizations
UNION ALL
SELECT
    'Group'::text,
    count(*)::int,
    0::int,
    NULL::timestamp,
    NULL::timestamp,
    '{}'::jsonb
FROM groups
UNION ALL
SELECT
    'Encounter'::text,
    count(*)::int,
    count(subject_ref)::int,
    min(period_start),
    max(COALESCE(period_end, period_start)),
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM encounters
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM encounters
UNION ALL
SELECT
    'Condition'::text,
    count(*)::int,
    count(subject_ref)::int,
    min(onset_date),
    max(onset_date),
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(clinical_status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM conditions
            GROUP BY COALESCE(clinical_status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM conditions
UNION ALL
SELECT
    'Observation'::text,
    count(*)::int,
    count(subject_ref)::int,
    min(effective_date),
    max(effective_date),
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM observations
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM observations
UNION ALL
SELECT
    'Procedure'::text,
    count(*)::int,
    count(subject_ref)::int,
    NULL::timestamp,
    NULL::timestamp,
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM procedures
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM procedures
UNION ALL
SELECT
    'MedicationRequest'::text,
    count(*)::int,
    count(subject_ref)::int,
    NULL::timestamp,
    NULL::timestamp,
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM medication_requests
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM medication_requests
UNION ALL
SELECT
    'AllergyIntolerance'::text,
    count(*)::int,
    count(patient_ref)::int,
    NULL::timestamp,
    NULL::timestamp,
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(clinical_status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM allergy_intolerances
            GROUP BY COALESCE(clinical_status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM allergy_intolerances
UNION ALL
SELECT
    'Immunization'::text,
    count(*)::int,
    count(patient_ref)::int,
    min(occurrence_date),
    max(occurrence_date),
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM immunizations
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM immunizations
UNION ALL
SELECT
    'CarePlan'::text,
    count(*)::int,
    count(subject_ref)::int,
    NULL::timestamp,
    NULL::timestamp,
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM care_plans
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM care_plans
UNION ALL
SELECT
    'CareTeam'::text,
    count(*)::int,
    count(subject_ref)::int,
    NULL::timestamp,
    NULL::timestamp,
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM care_teams
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM care_teams
UNION ALL
SELECT
    'Claim'::text,
    count(*)::int,
    count(patient_ref)::int,
    min(created),
    max(created),
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM claims
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM claims
UNION ALL
SELECT
    'ExplanationOfBenefit'::text,
    count(*)::int,
    count(patient_ref)::int,
    min(created),
    max(created),
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM explanation_of_benefits
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM explanation_of_benefits
UNION ALL
SELECT
    'DiagnosticReport'::text,
    count(*)::int,
    count(subject_ref)::int,
    min(effective_date),
    max(effective_date),
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM diagnostic_reports
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM diagnostic_reports
UNION ALL
SELECT
    'DocumentReference'::text,
    count(*)::int,
    count(subject_ref)::int,
    min(date),
    max(date),
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM document_references
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM document_references
UNION ALL
SELECT
    'Device'::text,
    count(*)::int,
    count(patient_ref)::int,
    NULL::timestamp,
    NULL::timestamp,
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM devices
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM devices
UNION ALL
SELECT
    'ImagingStudy'::text,
    count(*)::int,
    count(subject_ref)::int,
    min(started),
    max(started),
    COALESCE((
        SELECT jsonb_object_agg(status_key, status_count)
        FROM (
            SELECT COALESCE(status, 'unknown') AS status_key, count(*)::int AS status_count
            FROM imaging_studies
            GROUP BY COALESCE(status, 'unknown')
        ) status_counts
    ), '{}'::jsonb)
FROM imaging_studies;

CREATE OR REPLACE VIEW dashboard_group_summary_v AS
WITH group_members AS (
    SELECT
        g.id AS group_id,
        CASE
            WHEN member.member_ref LIKE 'Patient/%' THEN member.member_ref
            WHEN member.member_ref LIKE 'urn:uuid:%' THEN 'Patient/' || substring(member.member_ref FROM 10)
            ELSE member.member_ref
        END AS patient_ref
    FROM groups g
    LEFT JOIN LATERAL (
        SELECT member_elem -> 'entity' ->> 'reference' AS member_ref
        FROM jsonb_array_elements(COALESCE(g.resource_text::jsonb -> 'member', '[]'::jsonb)) AS member_elem
    ) member ON TRUE
),
distinct_group_patients AS (
    SELECT DISTINCT group_id, patient_ref
    FROM group_members
    WHERE patient_ref IS NOT NULL AND patient_ref <> ''
)
SELECT
    g.id AS group_id,
    g.name AS group_name,
    g.identifier_value,
    COALESCE(
        NULLIF(g.resource_text::jsonb ->> 'quantity', '')::int,
        (SELECT count(*)::int FROM distinct_group_patients dgp WHERE dgp.group_id = g.id),
        0
    )::int AS quantity,
    COALESCE((SELECT count(*)::int FROM distinct_group_patients dgp WHERE dgp.group_id = g.id), 0)::int AS member_count,
    COALESCE((
        SELECT count(*)::int
        FROM patients p
        WHERE 'Patient/' || p.id IN (
            SELECT dgp.patient_ref
            FROM distinct_group_patients dgp
            WHERE dgp.group_id = g.id
        )
    ), 0)::int AS patient_count,
    COALESCE((
        SELECT count(*)::int
        FROM encounters e
        WHERE e.subject_ref IN (
            SELECT dgp.patient_ref
            FROM distinct_group_patients dgp
            WHERE dgp.group_id = g.id
        )
    ), 0)::int AS encounter_count,
    COALESCE((
        SELECT count(*)::int
        FROM observations o
        WHERE o.subject_ref IN (
            SELECT dgp.patient_ref
            FROM distinct_group_patients dgp
            WHERE dgp.group_id = g.id
        )
    ), 0)::int AS observation_count,
    COALESCE((
        SELECT count(*)::int
        FROM claims c
        WHERE c.patient_ref IN (
            SELECT dgp.patient_ref
            FROM distinct_group_patients dgp
            WHERE dgp.group_id = g.id
        )
    ), 0)::int AS claim_count,
    COALESCE((
        SELECT count(*)::int
        FROM medication_requests mr
        WHERE mr.subject_ref IN (
            SELECT dgp.patient_ref
            FROM distinct_group_patients dgp
            WHERE dgp.group_id = g.id
        )
    ), 0)::int AS medication_request_count,
    COALESCE((
        SELECT count(*)::int
        FROM diagnostic_reports dr
        WHERE dr.subject_ref IN (
            SELECT dgp.patient_ref
            FROM distinct_group_patients dgp
            WHERE dgp.group_id = g.id
        )
    ), 0)::int AS diagnostic_report_count
FROM groups g;

CREATE OR REPLACE VIEW dashboard_observation_codes_v AS
SELECT
    o.code_value AS code,
    COALESCE(
        NULLIF(o.resource_text::jsonb #>> '{code,coding,0,display}', ''),
        NULLIF(o.resource_text::jsonb #>> '{code,text}', ''),
        NULLIF(o.code_value, ''),
        'Unknown'
    ) AS display,
    count(*)::int AS observation_count,
    min(o.effective_date) AS first_seen,
    max(o.effective_date) AS latest_seen
FROM observations o
GROUP BY
    o.code_value,
    COALESCE(
        NULLIF(o.resource_text::jsonb #>> '{code,coding,0,display}', ''),
        NULLIF(o.resource_text::jsonb #>> '{code,text}', ''),
        NULLIF(o.code_value, ''),
        'Unknown'
    );

CREATE OR REPLACE VIEW dashboard_quality_v AS
SELECT
    'groups.missing_quantity'::text AS metric_key,
    'Groups missing quantity in raw JSON'::text AS metric_label,
    'warning'::text AS severity,
    count(*)::int AS affected_count,
    'groups.resource_text.quantity is null or empty'::text AS detail
FROM groups
WHERE NULLIF(resource_text::jsonb ->> 'quantity', '') IS NULL
UNION ALL
SELECT
    'groups.member_count_mismatch'::text,
    'Group quantity does not match actual member count'::text,
    'warning'::text,
    count(*)::int,
    'quantity differs from member[].entity.reference count'::text
FROM (
    SELECT
        g.id,
        NULLIF(g.resource_text::jsonb ->> 'quantity', '')::int AS declared_quantity,
        COALESCE(jsonb_array_length(COALESCE(g.resource_text::jsonb -> 'member', '[]'::jsonb)), 0) AS actual_members
    FROM groups g
) group_counts
WHERE declared_quantity IS NOT NULL AND declared_quantity <> actual_members
UNION ALL
SELECT
    'observations.missing_subject'::text,
    'Observations missing subject references'::text,
    'error'::text,
    count(*)::int,
    'observations.subject_ref is null or empty'::text
FROM observations
WHERE COALESCE(subject_ref, '') = ''
UNION ALL
SELECT
    'observations.missing_code'::text,
    'Observations missing codes'::text,
    'error'::text,
    count(*)::int,
    'observations.code_value is null or empty'::text
FROM observations
WHERE COALESCE(code_value, '') = ''
UNION ALL
SELECT
    'observations.missing_date'::text,
    'Observations missing effective dates'::text,
    'warning'::text,
    count(*)::int,
    'observations.effective_date is null'::text
FROM observations
WHERE effective_date IS NULL
UNION ALL
SELECT
    'claims.missing_patient'::text,
    'Claims missing patient references'::text,
    'error'::text,
    count(*)::int,
    'claims.patient_ref is null or empty'::text
FROM claims
WHERE COALESCE(patient_ref, '') = ''
UNION ALL
SELECT
    'records.id_mismatch'::text,
    'Stored row id differs from resource_text.id'::text,
    'error'::text,
    count(*)::int,
    'row id and raw JSON id should match'::text
FROM (
    SELECT id, resource_text FROM patients
    UNION ALL SELECT id, resource_text FROM groups
    UNION ALL SELECT id, resource_text FROM encounters
    UNION ALL SELECT id, resource_text FROM observations
    UNION ALL SELECT id, resource_text FROM claims
    UNION ALL SELECT id, resource_text FROM medication_requests
    UNION ALL SELECT id, resource_text FROM diagnostic_reports
) raw_rows
WHERE COALESCE(raw_rows.resource_text::jsonb ->> 'id', '') <> raw_rows.id;

CREATE OR REPLACE VIEW dashboard_export_jobs_v AS
SELECT
    j.id AS job_id,
    j.group_id,
    g.name AS group_name,
    j.status,
    j.request_url,
    j.types,
    j.created_at,
    j.completed_at,
    j.expires_at,
    j.progress
FROM bulk_export_jobs j
LEFT JOIN groups g ON g.id = j.group_id;

CREATE OR REPLACE VIEW dashboard_overview_v AS
WITH clinical_window AS (
    SELECT
        min(first_clinical_date) AS first_clinical_date,
        max(latest_clinical_date) AS latest_clinical_date
    FROM dashboard_resource_coverage_v
    WHERE resource_type NOT IN ('Patient', 'Practitioner', 'Organization', 'Group')
)
SELECT
    COALESCE((SELECT resource_count FROM dashboard_resource_coverage_v WHERE resource_type = 'Patient'), 0)::int AS patient_count,
    COALESCE((SELECT resource_count FROM dashboard_resource_coverage_v WHERE resource_type = 'Group'), 0)::int AS group_count,
    COALESCE((SELECT sum(resource_count)::int FROM dashboard_resource_coverage_v), 0)::int AS resource_total_count,
    clinical_window.first_clinical_date,
    clinical_window.latest_clinical_date,
    COALESCE((SELECT count(*)::int FROM bulk_export_jobs WHERE status IN ('pending', 'in_progress')), 0)::int AS active_export_count,
    COALESCE((SELECT count(*)::int FROM bulk_export_jobs WHERE status = 'completed'), 0)::int AS completed_export_count
FROM clinical_window;

"""

    let createSchema (connString: string) =
        task {
            use conn = Connection.createConnection connString
            do! conn.OpenAsync()
            use cmd = new NpgsqlCommand(ddl, conn)
            let! _ = cmd.ExecuteNonQueryAsync()
            return ()
        }
