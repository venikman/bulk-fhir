module BulkFhir.Tests.ResourceTests

open System.Net
open System.Net.Http
open Expecto
open Swensen.Unquote
open BulkFhir.Tests.Fixtures

let tests (fixture: TestFixture) =
    let mutable discoveredPatientId = ""
    let mutable discoveredPatientName = ""
    let mutable discoveredPatientGender = ""
    let mutable discoveredConditionCode = ""
    let mutable discoveredConditionSystem = ""
    let mutable discoveredConditionPatientRef = ""

    testSequenced (testList "Resource Read/Search" [
        testTask "discover a Patient via search" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Patient")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>

            let entry = doc.RootElement.GetProperty("entry").[0].GetProperty("resource")
            discoveredPatientId <- entry.GetProperty("id").GetString()

            try discoveredPatientName <- entry.GetProperty("name").[0].GetProperty("family").GetString()
            with _ -> ()

            try discoveredPatientGender <- entry.GetProperty("gender").GetString()
            with _ -> ()
        }

        testTask "GET /fhir/Patient/{id} returns patient" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync($"/fhir/Patient/{discoveredPatientId}")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("resourceType").GetString() = "Patient" @>
            test <@ doc.RootElement.GetProperty("id").GetString() = discoveredPatientId @>
        }

        testTask "GET /fhir/Patient/{id} with unknown ID returns 404" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Patient/nonexistent-patient-id-99999")
            test <@ resp.StatusCode = HttpStatusCode.NotFound @>
        }

        testTask "GET /fhir/Patient?name=<discovered> returns matching patients" {
            test <@ discoveredPatientName <> "" @>
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync($"/fhir/Patient?name={discoveredPatientName}")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
        }

        testTask "GET /fhir/Patient?gender=<discovered> filters by gender" {
            test <@ discoveredPatientGender <> "" @>
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync($"/fhir/Patient?gender={discoveredPatientGender}")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
        }

        testTask "GET /fhir/Patient?birthdate=ge1900-01-01 filters by date" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Patient?birthdate=ge1900-01-01")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 0 @>
        }

        testTask "discover a Condition via search" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Condition")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            let total = doc.RootElement.GetProperty("total").GetInt32()

            if total >= 1 then
                let entry = doc.RootElement.GetProperty("entry").[0].GetProperty("resource")
                let coding = entry.GetProperty("code").GetProperty("coding").[0]
                discoveredConditionSystem <- coding.GetProperty("system").GetString()
                discoveredConditionCode <- coding.GetProperty("code").GetString()
                discoveredConditionPatientRef <- entry.GetProperty("subject").GetProperty("reference").GetString()
        }

        testTask "GET /fhir/Condition?code=system|code returns conditions" {
            test <@ discoveredConditionSystem <> "" @>
            let! (resp: HttpResponseMessage) =
                fixture.Client.GetAsync($"/fhir/Condition?code={discoveredConditionSystem}|{discoveredConditionCode}")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
        }

        testTask "GET /fhir/Condition?patient=ref returns patient conditions" {
            test <@ discoveredConditionPatientRef <> "" @>
            let! (resp: HttpResponseMessage) =
                fixture.Client.GetAsync($"/fhir/Condition?patient={discoveredConditionPatientRef}")
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
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 0 @>
        }
    ])
