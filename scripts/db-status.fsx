#r "nuget: Npgsql"
#r "nuget: Dapper"

open System
open Npgsql
open Dapper

let connString =
    Environment.GetEnvironmentVariable("CONNECTION_STRING")
    |> Option.ofObj
    |> Option.defaultValue "Host=localhost;Port=5433;Database=bulk_fhir;Username=postgres;Password=postgres"

try
    use conn = new NpgsqlConnection(connString)
    conn.Open()

    let tableCount =
        conn.ExecuteScalar<int>(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'")

    if tableCount = 0 then
        printfn "No tables found. Run the import first."
    else
        printfn "BulkFhir Database Status (%d tables)" tableCount
        printfn ""

        let tables =
            [ "patients"; "encounters"; "observations"; "conditions"; "procedures"
              "medication_requests"; "claims"; "explanation_of_benefits"; "immunizations"
              "care_plans"; "care_teams"; "organizations"; "practitioners"
              "diagnostic_reports"; "document_references"; "allergy_intolerances"
              "devices"; "imaging_studies"; "groups" ]

        let mutable total = 0
        for table in tables do
            try
                let count = conn.ExecuteScalar<int>(sprintf "SELECT count(*) FROM %s" table)
                if count > 0 then
                    printfn "  %-25s %6d" table count
                total <- total + count
            with _ -> ()

        printfn "  %-25s %6d" "─── TOTAL" total

with ex ->
    eprintfn "Error: %s" ex.Message
    exit 1
