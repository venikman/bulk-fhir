module BulkFhir.Tests.ResourceTests

open System.Net
open System.Net.Http
open System.Text.Json
open Expecto
open Swensen.Unquote
open BulkFhir.Tests.Fixtures

let private readBody (resp: HttpResponseMessage) = resp.Content.ReadAsStringAsync()
let private parseJson (s: string) = JsonDocument.Parse(s)

let tests (fixture: TestFixture) =
    testList "Resource Read/Search" [
        testTask "GET /fhir/Patient/{id} returns patient" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Patient/test-patient-1")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("resourceType").GetString() = "Patient" @>
            test <@ doc.RootElement.GetProperty("id").GetString() = "test-patient-1" @>
        }

        testTask "GET /fhir/Patient/{id} with unknown ID returns 404" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Patient/nonexistent")
            test <@ resp.StatusCode = HttpStatusCode.NotFound @>
        }

        testTask "GET /fhir/Patient?name=Smith returns matching patients" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Patient?name=Smith")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
        }

        testTask "GET /fhir/Patient?gender=male filters by gender" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Patient?gender=male")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
        }

        testTask "GET /fhir/Patient?birthdate=ge1990-01-01 filters by date" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Patient?birthdate=ge1990-01-01")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            // Should include test-patient-1 (1990-05-15) but not test-patient-2 (1985-03-20)
            test <@ doc.RootElement.GetProperty("total").GetInt32() = 1 @>
        }

        testTask "GET /fhir/Condition?code=http://snomed.info/sct|44054006 returns conditions" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Condition?code=http://snomed.info/sct|44054006")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
        }

        testTask "GET /fhir/Condition?patient=Patient/test-patient-1 returns patient conditions" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Condition?patient=Patient/test-patient-1")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
        }

        testTask "GET /fhir/UnknownType returns 404" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/FakeResource")
            test <@ resp.StatusCode = HttpStatusCode.NotFound @>
        }

        testTask "GET /fhir/Organization returns all organizations" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Organization")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
        }
    ]
