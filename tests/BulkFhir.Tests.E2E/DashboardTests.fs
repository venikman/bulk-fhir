module BulkFhir.Tests.DashboardTests

open System
open System.Net
open System.Net.Http
open System.Text.Json
open Expecto
open Swensen.Unquote
open BulkFhir.Tests.Fixtures

let private dashboardKey = "test-dashboard-key"

let private createDashboardRequest (path: string) =
    let req = new HttpRequestMessage(HttpMethod.Get, path)
    req.Headers.Add("x-dashboard-internal-key", dashboardKey)
    req

let tests (fixture: TestFixture) =
    let mutable discoveredPatientId = ""
    let mutable discoveredGroupId = ""

    testSequenced (testList "Dashboard API" [
        testTask "GET /dashboard-api/v1/overview without internal key returns 401" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/dashboard-api/v1/overview")
            test <@ resp.StatusCode = HttpStatusCode.Unauthorized @>
        }

        testTask "discover a Patient and Group for dashboard drilldowns" {
            let! (patientResp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Patient?_count=1")
            test <@ patientResp.StatusCode = HttpStatusCode.OK @>
            let! patientBody = readBody patientResp
            let patientDoc = parseJson patientBody
            discoveredPatientId <-
                patientDoc.RootElement.GetProperty("entry").[0].GetProperty("resource").GetProperty("id").GetString()

            let! (groupResp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group?name=a")
            test <@ groupResp.StatusCode = HttpStatusCode.OK @>
            let! groupBody = readBody groupResp
            let groupDoc = parseJson groupBody
            discoveredGroupId <-
                groupDoc.RootElement.GetProperty("entry").[0].GetProperty("resource").GetProperty("id").GetString()
        }

        testTask "GET /dashboard-api/v1/overview returns overview payload" {
            let req = createDashboardRequest "/dashboard-api/v1/overview"
            let! (resp: HttpResponseMessage) = fixture.Client.SendAsync(req)
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("resourceCounts").GetArrayLength() >= 1 @>
            test <@ doc.RootElement.GetProperty("topObservationCodes").GetArrayLength() >= 1 @>
            test <@ doc.RootElement.GetProperty("alerts").GetArrayLength() >= 1 @>
        }

        testTask "GET /dashboard-api/v1/resources returns resource coverage payload" {
            let req = createDashboardRequest "/dashboard-api/v1/resources"
            let! (resp: HttpResponseMessage) = fixture.Client.SendAsync(req)
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("resources").GetArrayLength() >= 5 @>
            test <@ doc.RootElement.GetProperty("topObservationCodes").GetArrayLength() >= 1 @>
        }

        testTask "GET /dashboard-api/v1/groups returns group summaries" {
            let req = createDashboardRequest "/dashboard-api/v1/groups"
            let! (resp: HttpResponseMessage) = fixture.Client.SendAsync(req)
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            let groups = doc.RootElement.GetProperty("groups")
            let mutable quantity = Unchecked.defaultof<JsonElement>
            Expect.isGreaterThanOrEqual (groups.GetArrayLength()) 1 "expected at least one group"
            Expect.isTrue (groups.[0].TryGetProperty("quantity", &quantity)) "expected quantity property"
        }

        testTask "kick-off bulk export and surface it through /dashboard-api/v1/exports" {
            let kickoffPath =
                $"/fhir/Group/{discoveredGroupId}/$davinci-data-export?exportType=hl7.fhir.us.davinci-atr&_type=Group,Patient"

            let! (kickoff: HttpResponseMessage) = fixture.Client.GetAsync(kickoffPath)
            test <@ kickoff.StatusCode = HttpStatusCode.Accepted @>

            let req = createDashboardRequest "/dashboard-api/v1/exports"
            let! (resp: HttpResponseMessage) = fixture.Client.SendAsync(req)
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            let exports = doc.RootElement.GetProperty("exports")
            Expect.isGreaterThanOrEqual (exports.GetArrayLength()) 1 "expected at least one export"
        }

        testTask "GET /dashboard-api/v1/quality returns quality metrics" {
            let req = createDashboardRequest "/dashboard-api/v1/quality"
            let! (resp: HttpResponseMessage) = fixture.Client.SendAsync(req)
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("metrics").GetArrayLength() >= 1 @>
        }

        testTask "GET /dashboard-api/v1/resource/Patient/{id} returns raw drilldown payload" {
            let req = createDashboardRequest $"/dashboard-api/v1/resource/Patient/{discoveredPatientId}"
            let! (resp: HttpResponseMessage) = fixture.Client.SendAsync(req)
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("resourceType").GetString() = "Patient" @>
            test <@ doc.RootElement.GetProperty("id").GetString() = discoveredPatientId @>
            test <@ doc.RootElement.GetProperty("summary").GetArrayLength() >= 1 @>
            test <@ doc.RootElement.GetProperty("rawJson").GetString().Contains("\"resourceType\"") @>
        }
    ])
