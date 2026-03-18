open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open System
open Falco
open Falco.Routing
open BulkFhir.Api
open BulkFhir.Storage

let endpoints =
    [
        get "/health" Handlers.health
        get "/fhir/metadata" Handlers.metadata

        // Group
        get "/fhir/Group" Handlers.groupSearch
        get "/fhir/Group/{id}" Handlers.groupRead

        // Bulk export
        get "/fhir/Group/{id}/$davinci-data-export" Handlers.bulkExportKickoff
        get "/fhir/bulk-status/{jobId}" Handlers.bulkStatusPoll
        delete "/fhir/bulk-status/{jobId}" Handlers.bulkStatusDelete
        get "/fhir/bulk-files/{jobId}/{fileName}" Handlers.bulkFileDownload

        // Generic resource
        get "/fhir/{resourceType}" Handlers.resourceSearch
        get "/fhir/{resourceType}/{id}" Handlers.resourceRead
    ]

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    builder.Services.AddRouting() |> ignore
    let connString = builder.Configuration.GetConnectionString("DefaultConnection")
    if not (String.IsNullOrWhiteSpace connString) then
        Schema.createSchema connString |> fun task -> task.GetAwaiter().GetResult()
    let app = builder.Build()
    app.UseRouting() |> ignore
    app.UseFalco(endpoints) |> ignore
    app.Run()
    0
