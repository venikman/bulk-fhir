open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Falco
open Falco.Routing
open BulkFhir.Api

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
    let app = builder.Build()
    app.UseRouting() |> ignore
    app.UseFalco(endpoints) |> ignore
    app.Run()
    0
