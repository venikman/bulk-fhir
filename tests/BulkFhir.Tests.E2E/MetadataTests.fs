module BulkFhir.Tests.MetadataTests

open System.Net
open System.Net.Http
open Expecto
open Swensen.Unquote
open BulkFhir.Tests.Fixtures

let tests (fixture: TestFixture) =
    testList "Metadata" [
        testTask "GET /fhir/metadata returns CapabilityStatement" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/metadata")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("resourceType").GetString() = "CapabilityStatement" @>
            test <@ doc.RootElement.GetProperty("fhirVersion").GetString() = "4.0.1" @>
        }

        testTask "GET /fhir/metadata has correct content-type" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/metadata")
            let ct: string = resp.Content.Headers.ContentType.ToString()
            test <@ ct.Contains("application/fhir+json") @>
        }
    ]
