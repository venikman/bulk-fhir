module BulkFhir.Tests.GroupTests

open System.Net
open System.Net.Http
open Expecto
open Swensen.Unquote
open BulkFhir.Tests.Fixtures

let tests (fixture: TestFixture) =
    let mutable discoveredId = ""
    let mutable discoveredName = ""
    let mutable discoveredIdentifierSystem = ""
    let mutable discoveredIdentifierValue = ""

    testSequenced (testList "Group" [
        testTask "discover a group via name search" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group?name=a")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            let total = doc.RootElement.GetProperty("total").GetInt32()
            test <@ total >= 1 @>
            test <@ doc.RootElement.GetProperty("type").GetString() = "searchset" @>

            let entry = doc.RootElement.GetProperty("entry").[0].GetProperty("resource")
            discoveredId <- entry.GetProperty("id").GetString()
            discoveredName <- entry.GetProperty("name").GetString()

            try
                let identifiers = entry.GetProperty("identifier")
                if identifiers.GetArrayLength() > 0 then
                    let ident = identifiers.[0]
                    discoveredIdentifierSystem <- ident.GetProperty("system").GetString()
                    discoveredIdentifierValue <- ident.GetProperty("value").GetString()
            with :? System.Collections.Generic.KeyNotFoundException -> ()
        }

        testTask "GET /fhir/Group?identifier=system|value returns group" {
            if discoveredIdentifierSystem <> "" then
                let! (resp: HttpResponseMessage) =
                    fixture.Client.GetAsync($"/fhir/Group?identifier={discoveredIdentifierSystem}|{discoveredIdentifierValue}")
                test <@ resp.StatusCode = HttpStatusCode.OK @>
                let! body = readBody resp
                let doc = parseJson body
                test <@ doc.RootElement.GetProperty("total").GetInt32() >= 1 @>
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
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync($"/fhir/Group/{discoveredId}")
            test <@ resp.StatusCode = HttpStatusCode.OK @>
            let! body = readBody resp
            let doc = parseJson body
            test <@ doc.RootElement.GetProperty("resourceType").GetString() = "Group" @>
            test <@ doc.RootElement.GetProperty("id").GetString() = discoveredId @>
        }

        testTask "GET /fhir/Group/{id} with unknown ID returns 404" {
            let! (resp: HttpResponseMessage) = fixture.Client.GetAsync("/fhir/Group/nonexistent-group-id-99999")
            test <@ resp.StatusCode = HttpStatusCode.NotFound @>
        }
    ])
