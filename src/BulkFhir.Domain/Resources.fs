namespace BulkFhir.Domain

open System.Text.Json.Serialization

// ─── Patient ────────────────────────────────────────

[<CLIMutable>]
type Patient =
    { [<JsonPropertyName("resourceType")>] ResourceType: string
      [<JsonPropertyName("id")>]           Id: string
      [<JsonPropertyName("text")>]         Text: Narrative option
      [<JsonPropertyName("extension")>]    Extension: Extension list
      [<JsonPropertyName("identifier")>]   Identifier: Identifier list
      [<JsonPropertyName("name")>]         Name: HumanName list
      [<JsonPropertyName("telecom")>]      Telecom: ContactPoint list
      [<JsonPropertyName("gender")>]       Gender: string option
      [<JsonPropertyName("birthDate")>]    BirthDate: string option
      [<JsonPropertyName("address")>]      Address: Address list
      [<JsonPropertyName("maritalStatus")>]       MaritalStatus: CodeableConcept option
      [<JsonPropertyName("multipleBirthBoolean")>] MultipleBirthBoolean: bool option
      [<JsonPropertyName("communication")>]       Communication: Communication list }

// ─── Practitioner ───────────────────────────────────

[<CLIMutable>]
type Practitioner =
    { [<JsonPropertyName("resourceType")>] ResourceType: string
      [<JsonPropertyName("id")>]           Id: string
      [<JsonPropertyName("active")>]       Active: bool option
      [<JsonPropertyName("name")>]         Name: HumanName list
      [<JsonPropertyName("telecom")>]      Telecom: ContactPoint list
      [<JsonPropertyName("address")>]      Address: Address list
      [<JsonPropertyName("gender")>]       Gender: string option
      [<JsonPropertyName("identifier")>]   Identifier: Identifier list
      [<JsonPropertyName("extension")>]    Extension: Extension list }

// ─── Organization ───────────────────────────────────

[<CLIMutable>]
type Organization =
    { [<JsonPropertyName("resourceType")>] ResourceType: string
      [<JsonPropertyName("id")>]           Id: string
      [<JsonPropertyName("active")>]       Active: bool option
      [<JsonPropertyName("type")>]         Type: CodeableConcept list
      [<JsonPropertyName("name")>]         Name: string option
      [<JsonPropertyName("telecom")>]      Telecom: ContactPoint list
      [<JsonPropertyName("address")>]      Address: Address list
      [<JsonPropertyName("identifier")>]   Identifier: Identifier list
      [<JsonPropertyName("extension")>]    Extension: Extension list }

// ─── Group ──────────────────────────────────────────

[<CLIMutable>]
type GroupMember =
    { [<JsonPropertyName("entity")>] Entity: Reference option }

[<CLIMutable>]
type Group =
    { [<JsonPropertyName("resourceType")>] ResourceType: string
      [<JsonPropertyName("id")>]           Id: string
      [<JsonPropertyName("text")>]         Text: Narrative option
      [<JsonPropertyName("active")>]       Active: bool option
      [<JsonPropertyName("actual")>]       Actual: bool option
      [<JsonPropertyName("type")>]         Type: string option
      [<JsonPropertyName("name")>]         Name: string option
      [<JsonPropertyName("quantity")>]     Quantity: int option
      [<JsonPropertyName("member")>]       Member: GroupMember list }

// ─── Encounter ──────────────────────────────────────

[<CLIMutable>]
type EncounterParticipant =
    { [<JsonPropertyName("type")>]       Type: CodeableConcept list
      [<JsonPropertyName("period")>]     Period: Period option
      [<JsonPropertyName("individual")>] Individual: Reference option }

[<CLIMutable>]
type Encounter =
    { [<JsonPropertyName("resourceType")>] ResourceType: string
      [<JsonPropertyName("id")>]           Id: string
      [<JsonPropertyName("status")>]       Status: string option
      [<JsonPropertyName("class")>]        Class: EncounterClass option
      [<JsonPropertyName("type")>]         Type: CodeableConcept list
      [<JsonPropertyName("subject")>]      Subject: Reference option
      [<JsonPropertyName("participant")>]  Participant: EncounterParticipant list
      [<JsonPropertyName("period")>]       Period: Period option
      [<JsonPropertyName("reasonCode")>]   ReasonCode: CodeableConcept list
      [<JsonPropertyName("serviceProvider")>] ServiceProvider: Reference option }

// ─── Condition ──────────────────────────────────────

[<CLIMutable>]
type Condition =
    { [<JsonPropertyName("resourceType")>]      ResourceType: string
      [<JsonPropertyName("id")>]                Id: string
      [<JsonPropertyName("clinicalStatus")>]    ClinicalStatus: CodeableConcept option
      [<JsonPropertyName("verificationStatus")>] VerificationStatus: CodeableConcept option
      [<JsonPropertyName("code")>]              Code: CodeableConcept option
      [<JsonPropertyName("subject")>]           Subject: Reference option
      [<JsonPropertyName("encounter")>]         Encounter: Reference option
      [<JsonPropertyName("onsetDateTime")>]     OnsetDateTime: string option
      [<JsonPropertyName("recordedDate")>]      RecordedDate: string option }

// ─── Observation ────────────────────────────────────

[<CLIMutable>]
type Observation =
    { [<JsonPropertyName("resourceType")>]     ResourceType: string
      [<JsonPropertyName("id")>]               Id: string
      [<JsonPropertyName("status")>]           Status: string option
      [<JsonPropertyName("category")>]         Category: CodeableConcept list
      [<JsonPropertyName("code")>]             Code: CodeableConcept option
      [<JsonPropertyName("subject")>]          Subject: Reference option
      [<JsonPropertyName("encounter")>]        Encounter: Reference option
      [<JsonPropertyName("effectiveDateTime")>] EffectiveDateTime: string option
      [<JsonPropertyName("issued")>]           Issued: string option
      [<JsonPropertyName("valueQuantity")>]    ValueQuantity: Quantity option }

// ─── Procedure ──────────────────────────────────────

[<CLIMutable>]
type Procedure =
    { [<JsonPropertyName("resourceType")>]   ResourceType: string
      [<JsonPropertyName("id")>]             Id: string
      [<JsonPropertyName("status")>]         Status: string option
      [<JsonPropertyName("code")>]           Code: CodeableConcept option
      [<JsonPropertyName("subject")>]        Subject: Reference option
      [<JsonPropertyName("encounter")>]      Encounter: Reference option
      [<JsonPropertyName("performedPeriod")>] PerformedPeriod: Period option }

// ─── MedicationRequest ──────────────────────────────

[<CLIMutable>]
type MedicationRequest =
    { [<JsonPropertyName("resourceType")>]             ResourceType: string
      [<JsonPropertyName("id")>]                       Id: string
      [<JsonPropertyName("status")>]                   Status: string option
      [<JsonPropertyName("intent")>]                   Intent: string option
      [<JsonPropertyName("medicationCodeableConcept")>] MedicationCodeableConcept: CodeableConcept option
      [<JsonPropertyName("subject")>]                  Subject: Reference option
      [<JsonPropertyName("encounter")>]                Encounter: Reference option
      [<JsonPropertyName("requester")>]                Requester: Reference option
      [<JsonPropertyName("authoredOn")>]               AuthoredOn: string option }

// ─── AllergyIntolerance ─────────────────────────────

[<CLIMutable>]
type AllergyIntolerance =
    { [<JsonPropertyName("resourceType")>]      ResourceType: string
      [<JsonPropertyName("id")>]                Id: string
      [<JsonPropertyName("clinicalStatus")>]    ClinicalStatus: CodeableConcept option
      [<JsonPropertyName("verificationStatus")>] VerificationStatus: CodeableConcept option
      [<JsonPropertyName("type")>]              Type: string option
      [<JsonPropertyName("category")>]          Category: string list
      [<JsonPropertyName("criticality")>]       Criticality: string option
      [<JsonPropertyName("code")>]              Code: CodeableConcept option
      [<JsonPropertyName("patient")>]           Patient: Reference option
      [<JsonPropertyName("recordedDate")>]      RecordedDate: string option }

// ─── Immunization ───────────────────────────────────

[<CLIMutable>]
type Immunization =
    { [<JsonPropertyName("resourceType")>]       ResourceType: string
      [<JsonPropertyName("id")>]                 Id: string
      [<JsonPropertyName("status")>]             Status: string option
      [<JsonPropertyName("vaccineCode")>]        VaccineCode: CodeableConcept option
      [<JsonPropertyName("patient")>]            Patient: Reference option
      [<JsonPropertyName("encounter")>]          Encounter: Reference option
      [<JsonPropertyName("occurrenceDateTime")>] OccurrenceDateTime: string option
      [<JsonPropertyName("primarySource")>]      PrimarySource: bool option }

// ─── CarePlan ───────────────────────────────────────

[<CLIMutable>]
type CarePlanActivityDetail =
    { [<JsonPropertyName("code")>]   Code: CodeableConcept option
      [<JsonPropertyName("status")>] Status: string option }

[<CLIMutable>]
type CarePlanActivity =
    { [<JsonPropertyName("detail")>] Detail: CarePlanActivityDetail option }

[<CLIMutable>]
type CarePlan =
    { [<JsonPropertyName("resourceType")>] ResourceType: string
      [<JsonPropertyName("id")>]           Id: string
      [<JsonPropertyName("text")>]         Text: Narrative option
      [<JsonPropertyName("status")>]       Status: string option
      [<JsonPropertyName("intent")>]       Intent: string option
      [<JsonPropertyName("category")>]     Category: CodeableConcept list
      [<JsonPropertyName("subject")>]      Subject: Reference option
      [<JsonPropertyName("encounter")>]    Encounter: Reference option
      [<JsonPropertyName("period")>]       Period: Period option
      [<JsonPropertyName("careTeam")>]     CareTeam: Reference list
      [<JsonPropertyName("activity")>]     Activity: CarePlanActivity list }

// ─── CareTeam ───────────────────────────────────────

[<CLIMutable>]
type CareTeamParticipant =
    { [<JsonPropertyName("role")>]   Role: CodeableConcept list
      [<JsonPropertyName("member")>] Member: Reference option }

[<CLIMutable>]
type CareTeam =
    { [<JsonPropertyName("resourceType")>]       ResourceType: string
      [<JsonPropertyName("id")>]                 Id: string
      [<JsonPropertyName("status")>]             Status: string option
      [<JsonPropertyName("subject")>]            Subject: Reference option
      [<JsonPropertyName("encounter")>]          Encounter: Reference option
      [<JsonPropertyName("period")>]             Period: Period option
      [<JsonPropertyName("participant")>]        Participant: CareTeamParticipant list
      [<JsonPropertyName("managingOrganization")>] ManagingOrganization: Reference list }

// ─── Claim ──────────────────────────────────────────

[<CLIMutable>]
type ClaimDiagnosis =
    { [<JsonPropertyName("sequence")>]           Sequence: int option
      [<JsonPropertyName("diagnosisReference")>] DiagnosisReference: Reference option }

[<CLIMutable>]
type ClaimInsurance =
    { [<JsonPropertyName("sequence")>] Sequence: int option
      [<JsonPropertyName("focal")>]    Focal: bool option
      [<JsonPropertyName("coverage")>] Coverage: Reference option }

[<CLIMutable>]
type ClaimItem =
    { [<JsonPropertyName("sequence")>]          Sequence: int option
      [<JsonPropertyName("productOrService")>]  ProductOrService: CodeableConcept option
      [<JsonPropertyName("encounter")>]         Encounter: Reference list
      [<JsonPropertyName("servicedPeriod")>]    ServicedPeriod: Period option
      [<JsonPropertyName("locationCodeableConcept")>] LocationCodeableConcept: CodeableConcept option
      [<JsonPropertyName("category")>]          Category: CodeableConcept option }

[<CLIMutable>]
type Claim =
    { [<JsonPropertyName("resourceType")>]  ResourceType: string
      [<JsonPropertyName("id")>]            Id: string
      [<JsonPropertyName("status")>]        Status: string option
      [<JsonPropertyName("type")>]          Type: CodeableConcept option
      [<JsonPropertyName("use")>]           Use: string option
      [<JsonPropertyName("patient")>]       Patient: Reference option
      [<JsonPropertyName("billablePeriod")>] BillablePeriod: Period option
      [<JsonPropertyName("created")>]       Created: string option
      [<JsonPropertyName("provider")>]      Provider: Reference option
      [<JsonPropertyName("priority")>]      Priority: CodeableConcept option
      [<JsonPropertyName("diagnosis")>]     Diagnosis: ClaimDiagnosis list
      [<JsonPropertyName("insurance")>]     Insurance: ClaimInsurance list
      [<JsonPropertyName("item")>]          Item: ClaimItem list
      [<JsonPropertyName("total")>]         Total: Money option }

// ─── ExplanationOfBenefit ───────────────────────────

[<CLIMutable>]
type EobCareTeam =
    { [<JsonPropertyName("sequence")>] Sequence: int option
      [<JsonPropertyName("provider")>] Provider: Reference option
      [<JsonPropertyName("role")>]     Role: CodeableConcept option }

[<CLIMutable>]
type EobDiagnosis =
    { [<JsonPropertyName("sequence")>]           Sequence: int option
      [<JsonPropertyName("diagnosisReference")>] DiagnosisReference: Reference option
      [<JsonPropertyName("type")>]               Type: CodeableConcept list }

[<CLIMutable>]
type EobInsurance =
    { [<JsonPropertyName("focal")>]    Focal: bool option
      [<JsonPropertyName("coverage")>] Coverage: Reference option }

[<CLIMutable>]
type EobTotal =
    { [<JsonPropertyName("category")>] Category: CodeableConcept option
      [<JsonPropertyName("amount")>]   Amount: Money option }

[<CLIMutable>]
type EobPayment =
    { [<JsonPropertyName("amount")>] Amount: Money option }

[<CLIMutable>]
type EobItem =
    { [<JsonPropertyName("sequence")>]          Sequence: int option
      [<JsonPropertyName("category")>]          Category: CodeableConcept option
      [<JsonPropertyName("productOrService")>]  ProductOrService: CodeableConcept option
      [<JsonPropertyName("servicedPeriod")>]    ServicedPeriod: Period option
      [<JsonPropertyName("locationCodeableConcept")>] LocationCodeableConcept: CodeableConcept option
      [<JsonPropertyName("encounter")>]         Encounter: Reference list }

[<CLIMutable>]
type ExplanationOfBenefit =
    { [<JsonPropertyName("resourceType")>]  ResourceType: string
      [<JsonPropertyName("id")>]            Id: string
      [<JsonPropertyName("identifier")>]    Identifier: Identifier list
      [<JsonPropertyName("status")>]        Status: string option
      [<JsonPropertyName("type")>]          Type: CodeableConcept option
      [<JsonPropertyName("use")>]           Use: string option
      [<JsonPropertyName("patient")>]       Patient: Reference option
      [<JsonPropertyName("billablePeriod")>] BillablePeriod: Period option
      [<JsonPropertyName("created")>]       Created: string option
      [<JsonPropertyName("insurer")>]       Insurer: Reference option
      [<JsonPropertyName("provider")>]      Provider: Reference option
      [<JsonPropertyName("referral")>]      Referral: Reference option
      [<JsonPropertyName("claim")>]         Claim: Reference option
      [<JsonPropertyName("outcome")>]       Outcome: string option
      [<JsonPropertyName("careTeam")>]      CareTeam: EobCareTeam list
      [<JsonPropertyName("diagnosis")>]     Diagnosis: EobDiagnosis list
      [<JsonPropertyName("insurance")>]     Insurance: EobInsurance list
      [<JsonPropertyName("item")>]          Item: EobItem list
      [<JsonPropertyName("total")>]         Total: EobTotal list
      [<JsonPropertyName("payment")>]       Payment: EobPayment option }

// ─── DiagnosticReport ───────────────────────────────

[<CLIMutable>]
type DiagnosticReport =
    { [<JsonPropertyName("resourceType")>]     ResourceType: string
      [<JsonPropertyName("id")>]               Id: string
      [<JsonPropertyName("status")>]           Status: string option
      [<JsonPropertyName("category")>]         Category: CodeableConcept list
      [<JsonPropertyName("code")>]             Code: CodeableConcept option
      [<JsonPropertyName("subject")>]          Subject: Reference option
      [<JsonPropertyName("encounter")>]        Encounter: Reference option
      [<JsonPropertyName("effectiveDateTime")>] EffectiveDateTime: string option
      [<JsonPropertyName("issued")>]           Issued: string option
      [<JsonPropertyName("result")>]           Result: Reference list }

// ─── DocumentReference ──────────────────────────────

[<CLIMutable>]
type DocumentReferenceContentAttachment =
    { [<JsonPropertyName("contentType")>] ContentType: string option
      [<JsonPropertyName("url")>]         Url: string option }

[<CLIMutable>]
type DocumentReferenceContent =
    { [<JsonPropertyName("attachment")>] Attachment: DocumentReferenceContentAttachment option
      [<JsonPropertyName("format")>]    Format: Coding option }

[<CLIMutable>]
type DocumentReference =
    { [<JsonPropertyName("resourceType")>] ResourceType: string
      [<JsonPropertyName("id")>]           Id: string
      [<JsonPropertyName("meta")>]         Meta: Meta option
      [<JsonPropertyName("text")>]         Text: Narrative option
      [<JsonPropertyName("status")>]       Status: string option
      [<JsonPropertyName("type")>]         Type: CodeableConcept option
      [<JsonPropertyName("date")>]         Date: string option
      [<JsonPropertyName("description")>]  Description: string option
      [<JsonPropertyName("subject")>]      Subject: Reference option
      [<JsonPropertyName("author")>]       Author: Reference list
      [<JsonPropertyName("content")>]      Content: DocumentReferenceContent list }

// ─── Device ─────────────────────────────────────────

[<CLIMutable>]
type DeviceName =
    { [<JsonPropertyName("name")>] Name: string option
      [<JsonPropertyName("type")>] Type: string option }

[<CLIMutable>]
type UdiCarrier =
    { [<JsonPropertyName("deviceIdentifier")>] DeviceIdentifier: string option
      [<JsonPropertyName("carrierHRF")>]       CarrierHRF: string option }

[<CLIMutable>]
type Device =
    { [<JsonPropertyName("resourceType")>]        ResourceType: string
      [<JsonPropertyName("id")>]                  Id: string
      [<JsonPropertyName("status")>]              Status: string option
      [<JsonPropertyName("type")>]                Type: CodeableConcept option
      [<JsonPropertyName("patient")>]             Patient: Reference option
      [<JsonPropertyName("deviceName")>]          DeviceName: DeviceName list
      [<JsonPropertyName("serialNumber")>]        SerialNumber: string option
      [<JsonPropertyName("distinctIdentifier")>]  DistinctIdentifier: string option
      [<JsonPropertyName("lotNumber")>]           LotNumber: string option
      [<JsonPropertyName("manufactureDate")>]     ManufactureDate: string option
      [<JsonPropertyName("expirationDate")>]      ExpirationDate: string option
      [<JsonPropertyName("udiCarrier")>]          UdiCarrier: UdiCarrier list }

// ─── ImagingStudy ───────────────────────────────────

[<CLIMutable>]
type ImagingStudySeriesInstance =
    { [<JsonPropertyName("uid")>]      Uid: string option
      [<JsonPropertyName("sopClass")>] SopClass: Coding option
      [<JsonPropertyName("title")>]    Title: string option }

[<CLIMutable>]
type ImagingStudySeries =
    { [<JsonPropertyName("uid")>]                Uid: string option
      [<JsonPropertyName("modality")>]           Modality: Coding option
      [<JsonPropertyName("bodySite")>]           BodySite: Coding option
      [<JsonPropertyName("numberOfInstances")>]  NumberOfInstances: int option
      [<JsonPropertyName("instance")>]           Instance: ImagingStudySeriesInstance list }

[<CLIMutable>]
type ImagingStudy =
    { [<JsonPropertyName("resourceType")>]       ResourceType: string
      [<JsonPropertyName("id")>]                 Id: string
      [<JsonPropertyName("identifier")>]         Identifier: Identifier list
      [<JsonPropertyName("status")>]             Status: string option
      [<JsonPropertyName("subject")>]            Subject: Reference option
      [<JsonPropertyName("encounter")>]          Encounter: Reference option
      [<JsonPropertyName("started")>]            Started: string option
      [<JsonPropertyName("numberOfSeries")>]     NumberOfSeries: int option
      [<JsonPropertyName("numberOfInstances")>]  NumberOfInstances: int option
      [<JsonPropertyName("series")>]             Series: ImagingStudySeries list }
