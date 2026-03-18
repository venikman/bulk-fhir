module BulkFhir.Tests.HealthTests

open System.Net
open System.Net.Http
open System.Text.Json
open Expecto
open Swensen.Unquote
open BulkFhir.Tests.Fixtures

let private readBody (resp: HttpResponseMessage) = resp.Content.ReadAsStringAsync()
let private parseJson (s: string) = JsonDocument.Parse(s)

let tests (fixture: TestFixture) =
    testList "Health" [
        testTask "GET /health returns 200 with status ok" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/health")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            let status = doc.RootElement.GetProperty("status").GetString()
            test <@ status = "ok" @>
        }
    ]
