module BulkFhir.Tests.Fixtures

open System
open System.Net.Http
open System.Text.Json
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Falco
open Falco.Routing
open BulkFhir.Api
open BulkFhir.Storage

let readBody (resp: HttpResponseMessage) = resp.Content.ReadAsStringAsync()
let parseJson (s: string) = JsonDocument.Parse(s)

let private createTestServer (connString: string) =
    Schema.createSchema connString |> fun task -> task.GetAwaiter().GetResult()

    let endpoints =
        [
            get "/health" Handlers.health
            get "/fhir/metadata" Handlers.metadata
            get "/fhir/Group" Handlers.groupSearch
            get "/fhir/Group/{id}" Handlers.groupRead
            get "/fhir/Group/{id}/$davinci-data-export" Handlers.bulkExportKickoff
            get "/fhir/bulk-status/{jobId}" Handlers.bulkStatusPoll
            delete "/fhir/bulk-status/{jobId}" Handlers.bulkStatusDelete
            get "/fhir/bulk-files/{jobId}/{fileName}" Handlers.bulkFileDownload
            get "/fhir/{resourceType}" Handlers.resourceSearch
            get "/fhir/{resourceType}/{id}" Handlers.resourceRead
        ]

    let builder =
        WebHostBuilder()
            .ConfigureAppConfiguration(fun _ cfg ->
                cfg.AddInMemoryCollection(
                    dict [
                        "ConnectionStrings:DefaultConnection", connString
                    ]) |> ignore)
            .ConfigureServices(fun services ->
                services.AddRouting() |> ignore)
            .Configure(fun app ->
                app.UseRouting() |> ignore
                app.UseFalco(endpoints) |> ignore)
    new TestServer(builder)

type TestFixture() =
    let bulkFhirUrl = Environment.GetEnvironmentVariable("BULK_FHIR_URL") |> Option.ofObj
    let mutable server: TestServer option = None
    let mutable httpClient: HttpClient = Unchecked.defaultof<_>

    do
        match bulkFhirUrl with
        | Some url ->
            httpClient <- new HttpClient(BaseAddress = Uri(url))
        | None ->
            let connString =
                Environment.GetEnvironmentVariable("CONNECTION_STRING")
                |> Option.ofObj
                |> Option.defaultWith (fun () ->
                    failwith "CONNECTION_STRING is required in testing mode. Set BULK_FHIR_URL to run in verification mode instead.")
            let s = createTestServer connString
            server <- Some s
            httpClient <- s.CreateClient()

    member _.Client = httpClient
    member _.Mode = if bulkFhirUrl.IsSome then "verification" else "testing"

    member _.Dispose() =
        httpClient.Dispose()
        server |> Option.iter (fun s -> s.Dispose())
