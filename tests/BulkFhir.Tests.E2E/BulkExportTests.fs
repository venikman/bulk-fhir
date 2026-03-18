module BulkFhir.Tests.BulkExportTests

open System
open System.Net
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks
open Expecto
open Swensen.Unquote
open BulkFhir.Tests.Fixtures

let private readBody (resp: HttpResponseMessage) = resp.Content.ReadAsStringAsync()
let private parseJson (s: string) = JsonDocument.Parse(s)

let tests (fixture: TestFixture) =
    testList "Bulk Export" [
        testTask "kick-off without exportType returns 400" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group/test-group-1/$davinci-data-export?_type=Group,Patient")
            test <@ resp.StatusCode = HttpStatusCode.BadRequest @>
        }

        testTask "kick-off without _type returns 400" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group/test-group-1/$davinci-data-export?exportType=hl7.fhir.us.davinci-atr")
            test <@ resp.StatusCode = HttpStatusCode.BadRequest @>
        }

        testTask "kick-off with unknown group returns 404" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group/nonexistent/$davinci-data-export?exportType=hl7.fhir.us.davinci-atr&_type=Group,Patient")
            test <@ resp.StatusCode = HttpStatusCode.NotFound @>
        }

        testTask "full bulk export flow: kick-off, poll, download" {
            // 1. Kick off
            let! (kickoff: HttpResponseMessage) = fixture.Client.GetAsync(
                "/fhir/Group/test-group-1/$davinci-data-export?exportType=hl7.fhir.us.davinci-atr&_type=Group,Patient")
            test <@ kickoff.StatusCode = HttpStatusCode.Accepted @>

            let contentLocation: string = kickoff.Content.Headers.GetValues("Content-Location") |> Seq.head
            test <@ contentLocation.Contains("/fhir/bulk-status/") @>

            let statusPath = Uri(contentLocation).PathAndQuery

            // 2. Poll until complete (max 10 attempts)
            let mutable completed = false
            let mutable manifest = ""
            let mutable attempts = 0

            while not completed && attempts < 10 do
                do! Task.Delay(500)
                let! (poll: HttpResponseMessage) = fixture.Client.GetAsync(statusPath)

                if poll.StatusCode = HttpStatusCode.OK then
                    completed <- true
                    let! body = readBody poll
                    manifest <- body
                elif poll.StatusCode = HttpStatusCode.Accepted then
                    attempts <- attempts + 1
                else
                    failwith (sprintf "Unexpected status: %A" poll.StatusCode)

            test <@ completed @>

            // 3. Verify manifest
            let doc = parseJson manifest
            let output: JsonElement = doc.RootElement.GetProperty("output")
            let outputLen: int = output.GetArrayLength()
            test <@ outputLen >= 1 @>

            let types: string list =
                [ for i in 0 .. outputLen - 1 ->
                    output.[i].GetProperty("type").GetString() ]
            test <@ types |> List.contains "Group" @>
            test <@ types |> List.contains "Patient" @>

            // 4. Download each file
            for i in 0 .. outputLen - 1 do
                let fileUrl: string = output.[i].GetProperty("url").GetString()
                let filePath = Uri(fileUrl).PathAndQuery
                let! (fileResp: HttpResponseMessage) = fixture.Client.GetAsync(filePath)
                test <@ fileResp.StatusCode = HttpStatusCode.OK @>

                let ct: string = fileResp.Content.Headers.ContentType.ToString()
                test <@ ct.Contains("ndjson") @>

                let! (content: string) = readBody fileResp
                test <@ content.Length > 0 @>
        }

        testTask "DELETE /fhir/bulk-status/{jobId} cancels job" {
            let! (kickoff: HttpResponseMessage) = fixture.Client.GetAsync(
                "/fhir/Group/test-group-1/$davinci-data-export?exportType=hl7.fhir.us.davinci-atr&_type=Group,Patient")
            let contentLocation: string = kickoff.Content.Headers.GetValues("Content-Location") |> Seq.head
            let statusPath = Uri(contentLocation).PathAndQuery

            // Wait for export to complete so runExport doesn't overwrite our Expired status
            let mutable done' = false
            while not done' do
                do! Task.Delay(200)
                let! (check: HttpResponseMessage) = fixture.Client.GetAsync(statusPath)
                if check.StatusCode = HttpStatusCode.OK then done' <- true

            let! (deleteResp: HttpResponseMessage) = fixture.Client.DeleteAsync(statusPath)
            test <@ deleteResp.StatusCode = HttpStatusCode.Accepted @>

            let! (poll: HttpResponseMessage) = fixture.Client.GetAsync(statusPath)
            test <@ poll.StatusCode = HttpStatusCode.NotFound @>
        }

        testTask "GET /fhir/bulk-status/nonexistent returns 404" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/bulk-status/nonexistent")
            test <@ resp.StatusCode = HttpStatusCode.NotFound @>
        }
    ]
