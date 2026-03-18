namespace BulkFhir.Domain

open System
open System.Text.Json.Serialization

/// Base FHIR types shared across all resources.
/// Modeled from the actual Synthea R4 data shapes.

[<AutoOpen>]
module Common =

    [<CLIMutable>]
    type Coding =
        { [<JsonPropertyName("system")>]   System: string option
          [<JsonPropertyName("code")>]     Code: string option
          [<JsonPropertyName("display")>]  Display: string option }

    [<CLIMutable>]
    type CodeableConcept =
        { [<JsonPropertyName("coding")>] Coding: Coding list
          [<JsonPropertyName("text")>]   Text: string option }

    [<CLIMutable>]
    type Reference =
        { [<JsonPropertyName("reference")>] Reference: string option
          [<JsonPropertyName("display")>]   Display: string option }

    [<CLIMutable>]
    type Period =
        { [<JsonPropertyName("start")>] Start: string option
          [<JsonPropertyName("end")>]   End: string option }

    [<CLIMutable>]
    type Quantity =
        { [<JsonPropertyName("value")>]  Value: decimal option
          [<JsonPropertyName("unit")>]   Unit: string option
          [<JsonPropertyName("system")>] System: string option
          [<JsonPropertyName("code")>]   Code: string option }

    [<CLIMutable>]
    type Money =
        { [<JsonPropertyName("value")>]    Value: decimal option
          [<JsonPropertyName("currency")>] Currency: string option }

    [<CLIMutable>]
    type HumanName =
        { [<JsonPropertyName("use")>]    Use: string option
          [<JsonPropertyName("family")>] Family: string option
          [<JsonPropertyName("given")>]  Given: string list
          [<JsonPropertyName("prefix")>] Prefix: string list }

    [<CLIMutable>]
    type Address =
        { [<JsonPropertyName("line")>]       Line: string list
          [<JsonPropertyName("city")>]       City: string option
          [<JsonPropertyName("state")>]      State: string option
          [<JsonPropertyName("postalCode")>] PostalCode: string option
          [<JsonPropertyName("country")>]    Country: string option }

    [<CLIMutable>]
    type ContactPoint =
        { [<JsonPropertyName("system")>] System: string option
          [<JsonPropertyName("value")>]  Value: string option
          [<JsonPropertyName("use")>]    Use: string option }

    [<CLIMutable>]
    type Identifier =
        { [<JsonPropertyName("system")>] System: string option
          [<JsonPropertyName("value")>]  Value: string option
          [<JsonPropertyName("type")>]   Type: CodeableConcept option }

    [<CLIMutable>]
    type Extension =
        { [<JsonPropertyName("url")>]            Url: string option
          [<JsonPropertyName("valueString")>]    ValueString: string option
          [<JsonPropertyName("valueDecimal")>]   ValueDecimal: decimal option
          [<JsonPropertyName("valueCode")>]      ValueCode: string option
          [<JsonPropertyName("valueAddress")>]   ValueAddress: Address option
          [<JsonPropertyName("valueCoding")>]    ValueCoding: Coding option }

    [<CLIMutable>]
    type Narrative =
        { [<JsonPropertyName("status")>] Status: string option
          [<JsonPropertyName("div")>]    Div: string option }

    [<CLIMutable>]
    type Meta =
        { [<JsonPropertyName("profile")>]     Profile: string list
          [<JsonPropertyName("lastUpdated")>] LastUpdated: string option }

    /// Coding class for Encounter.class (which is a single Coding, not CodeableConcept)
    [<CLIMutable>]
    type EncounterClass =
        { [<JsonPropertyName("system")>] System: string option
          [<JsonPropertyName("code")>]   Code: string option }

    [<CLIMutable>]
    type Communication =
        { [<JsonPropertyName("language")>] Language: CodeableConcept option }
