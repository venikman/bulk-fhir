module BulkFhir.Tests.GroupTests

open System.Net
open System.Net.Http
open System.Text.Json
open Expecto
open Swensen.Unquote
open BulkFhir.Tests.Fixtures

let private readBody (resp: HttpResponseMessage) = resp.Content.ReadAsStringAsync()
let private parseJson (s: string) = JsonDocument.Parse(s)

let tests (fixture: TestFixture) =
    testList "Group" [
        testTask "GET /fhir/Group?name=Test returns matching groups" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group?name=Test")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
            test <@ doc.RootElement.GetProperty("type").GetString() = "searchset" @>
        }

        testTask "GET /fhir/Group?identifier=http://example.org|grp-001 returns group" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group?identifier=http://example.org|grp-001")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("total").GetInt32() = 1 @>
        }

        testTask "GET /fhir/Group without params returns 400" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group")
            test <@ resp.StatusCode = HttpStatusCode.BadRequest @>
        }

        testTask "GET /fhir/Group with both identifier and name returns 400" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group?identifier=x|y&name=z")
            test <@ resp.StatusCode = HttpStatusCode.BadRequest @>
        }

        testTask "GET /fhir/Group/{id} returns group by ID" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group/test-group-1")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("resourceType").GetString() = "Group" @>
            test <@ doc.RootElement.GetProperty("id").GetString() = "test-group-1" @>
        }

        testTask "GET /fhir/Group/{id} with unknown ID returns 404" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group/nonexistent")
            test <@ resp.StatusCode = HttpStatusCode.NotFound @>
        }
    ]
