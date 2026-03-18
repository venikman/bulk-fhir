namespace BulkFhir.Domain

/// Discriminated union representing all supported FHIR resource types.
/// Used for routing, search dispatch, and bulk export type filtering.
[<RequireQualifiedAccess>]
type FhirResourceType =
    | Patient
    | Practitioner
    | Organization
    | Group
    | Encounter
    | Condition
    | Observation
    | Procedure
    | MedicationRequest
    | AllergyIntolerance
    | Immunization
    | CarePlan
    | CareTeam
    | Claim
    | ExplanationOfBenefit
    | DiagnosticReport
    | DocumentReference
    | Device
    | ImagingStudy

module FhirResourceType =

    let fromString (s: string) =
        match s with
        | "Patient"              -> Some FhirResourceType.Patient
        | "Practitioner"         -> Some FhirResourceType.Practitioner
        | "Organization"         -> Some FhirResourceType.Organization
        | "Group"                -> Some FhirResourceType.Group
        | "Encounter"            -> Some FhirResourceType.Encounter
        | "Condition"            -> Some FhirResourceType.Condition
        | "Observation"          -> Some FhirResourceType.Observation
        | "Procedure"            -> Some FhirResourceType.Procedure
        | "MedicationRequest"    -> Some FhirResourceType.MedicationRequest
        | "AllergyIntolerance"   -> Some FhirResourceType.AllergyIntolerance
        | "Immunization"         -> Some FhirResourceType.Immunization
        | "CarePlan"             -> Some FhirResourceType.CarePlan
        | "CareTeam"             -> Some FhirResourceType.CareTeam
        | "Claim"                -> Some FhirResourceType.Claim
        | "ExplanationOfBenefit" -> Some FhirResourceType.ExplanationOfBenefit
        | "DiagnosticReport"     -> Some FhirResourceType.DiagnosticReport
        | "DocumentReference"    -> Some FhirResourceType.DocumentReference
        | "Device"               -> Some FhirResourceType.Device
        | "ImagingStudy"         -> Some FhirResourceType.ImagingStudy
        | _                      -> None

    let toString (rt: FhirResourceType) =
        match rt with
        | FhirResourceType.Patient              -> "Patient"
        | FhirResourceType.Practitioner         -> "Practitioner"
        | FhirResourceType.Organization         -> "Organization"
        | FhirResourceType.Group                -> "Group"
        | FhirResourceType.Encounter            -> "Encounter"
        | FhirResourceType.Condition            -> "Condition"
        | FhirResourceType.Observation          -> "Observation"
        | FhirResourceType.Procedure            -> "Procedure"
        | FhirResourceType.MedicationRequest    -> "MedicationRequest"
        | FhirResourceType.AllergyIntolerance   -> "AllergyIntolerance"
        | FhirResourceType.Immunization         -> "Immunization"
        | FhirResourceType.CarePlan             -> "CarePlan"
        | FhirResourceType.CareTeam             -> "CareTeam"
        | FhirResourceType.Claim                -> "Claim"
        | FhirResourceType.ExplanationOfBenefit -> "ExplanationOfBenefit"
        | FhirResourceType.DiagnosticReport     -> "DiagnosticReport"
        | FhirResourceType.DocumentReference    -> "DocumentReference"
        | FhirResourceType.Device               -> "Device"
        | FhirResourceType.ImagingStudy         -> "ImagingStudy"

    let tableName (rt: FhirResourceType) =
        match rt with
        | FhirResourceType.Patient              -> "patients"
        | FhirResourceType.Practitioner         -> "practitioners"
        | FhirResourceType.Organization         -> "organizations"
        | FhirResourceType.Group                -> "groups"
        | FhirResourceType.Encounter            -> "encounters"
        | FhirResourceType.Condition            -> "conditions"
        | FhirResourceType.Observation          -> "observations"
        | FhirResourceType.Procedure            -> "procedures"
        | FhirResourceType.MedicationRequest    -> "medication_requests"
        | FhirResourceType.AllergyIntolerance   -> "allergy_intolerances"
        | FhirResourceType.Immunization         -> "immunizations"
        | FhirResourceType.CarePlan             -> "care_plans"
        | FhirResourceType.CareTeam             -> "care_teams"
        | FhirResourceType.Claim                -> "claims"
        | FhirResourceType.ExplanationOfBenefit -> "explanation_of_benefits"
        | FhirResourceType.DiagnosticReport     -> "diagnostic_reports"
        | FhirResourceType.DocumentReference    -> "document_references"
        | FhirResourceType.Device               -> "devices"
        | FhirResourceType.ImagingStudy         -> "imaging_studies"

    let all =
        [ FhirResourceType.Patient; FhirResourceType.Practitioner; FhirResourceType.Organization
          FhirResourceType.Group; FhirResourceType.Encounter; FhirResourceType.Condition
          FhirResourceType.Observation; FhirResourceType.Procedure; FhirResourceType.MedicationRequest
          FhirResourceType.AllergyIntolerance; FhirResourceType.Immunization; FhirResourceType.CarePlan
          FhirResourceType.CareTeam; FhirResourceType.Claim; FhirResourceType.ExplanationOfBenefit
          FhirResourceType.DiagnosticReport; FhirResourceType.DocumentReference
          FhirResourceType.Device; FhirResourceType.ImagingStudy ]
