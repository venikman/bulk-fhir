namespace BulkFhir.Api

open System.IO
open System.Text.Json

/// FHIR response helpers: Bundle, OperationOutcome, CapabilityStatement.
module Fhir =

    let fhirContentType = "application/fhir+json; charset=utf-8"
    let ndjsonContentType = "application/fhir+ndjson; charset=utf-8"

    let operationOutcome (severity: string) (code: string) (diagnostics: string) =
        JsonSerializer.Serialize(
            {| resourceType = "OperationOutcome"
               issue = [| {| severity = severity; code = code; diagnostics = diagnostics |} |] |})

    /// Build a FHIR searchset Bundle using Utf8JsonWriter to correctly embed raw JSON resources.
    let searchBundle (baseUrl: string) (resourceType: string) (selfUrl: string) (resources: string list) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream)

        writer.WriteStartObject()
        writer.WriteString("resourceType", "Bundle")
        writer.WriteString("type", "searchset")
        writer.WriteNumber("total", resources.Length)

        writer.WriteStartArray("link")
        writer.WriteStartObject()
        writer.WriteString("relation", "self")
        writer.WriteString("url", selfUrl)
        writer.WriteEndObject()
        writer.WriteEndArray()

        writer.WriteStartArray("entry")
        for json in resources do
            use doc = JsonDocument.Parse(json)
            let id = doc.RootElement.GetProperty("id").GetString()
            writer.WriteStartObject()
            writer.WriteString("fullUrl", $"{baseUrl}/fhir/{resourceType}/{id}")
            writer.WritePropertyName("resource")
            doc.RootElement.WriteTo(writer)
            writer.WriteEndObject()
        writer.WriteEndArray()

        writer.WriteEndObject()
        writer.Flush()

        System.Text.Encoding.UTF8.GetString(stream.ToArray())

    let capabilityStatement (baseUrl: string) =
        let resourceTypes =
            [| "Patient"; "Practitioner"; "Organization"; "Group"; "Encounter"
               "Condition"; "Observation"; "Procedure"; "MedicationRequest"
               "AllergyIntolerance"; "Immunization"; "CarePlan"; "CareTeam"
               "Claim"; "ExplanationOfBenefit"; "DiagnosticReport"
               "DocumentReference"; "Device"; "ImagingStudy" |]

        let restResources =
            resourceTypes
            |> Array.map (fun rt ->
                {| ``type`` = rt
                   interaction = [| {| code = "read" |}; {| code = "search-type" |} |] |})

        JsonSerializer.Serialize(
            {| resourceType = "CapabilityStatement"
               status = "active"
               date = "2026-03-17"
               kind = "instance"
               fhirVersion = "4.0.1"
               format = [| "json" |]
               implementation = {| description = "BulkFhir F# Server"; url = baseUrl |}
               rest = [|
                   {| mode = "server"
                      resource = restResources
                      operation = [|
                          {| name = "davinci-data-export"
                             definition = "http://hl7.org/fhir/us/davinci-atr/OperationDefinition/davinci-data-export" |}
                      |] |}
               |] |},
            JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase))
