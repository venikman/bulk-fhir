open System
open Npgsql
open BulkFhir.Storage

let private waitForDb (connString: string) =
    printfn "Waiting for database..."
    let mutable ready = false
    let mutable attempts = 0
    while not ready && attempts < 15 do
        try
            use conn = new NpgsqlConnection(connString)
            conn.Open()
            conn.Close()
            ready <- true
        with _ ->
            attempts <- attempts + 1
            printfn $"  Attempt {attempts}/15 - database not ready, retrying in 2s..."
            Threading.Thread.Sleep(2000)
    if not ready then
        eprintfn "Error: Could not connect to database after 30 seconds."
        exit 1
    printfn "Database is ready."
    printfn ""

[<EntryPoint>]
let main args =
    let connString =
        match args |> Array.tryFind (fun a -> a.StartsWith("--connection-string=")) with
        | Some cs -> cs.["--connection-string=".Length..]
        | None ->
            Environment.GetEnvironmentVariable("CONNECTION_STRING")
            |> Option.ofObj
            |> Option.defaultValue "Host=localhost;Port=5433;Database=bulk_fhir;Username=postgres;Password=postgres"

    let dataDir =
        match args |> Array.tryFind (fun a -> a.StartsWith("--data-dir=")) with
        | Some d -> d.["--data-dir=".Length..]
        | None ->
            Environment.GetEnvironmentVariable("DATA_DIR")
            |> Option.ofObj
            |> Option.defaultValue "./downloads"

    printfn "BulkFhir Import Tool"
    printfn $"  Connection: {connString.[..30]}..."
    printfn $"  Data dir:   {dataDir}"
    printfn ""

    waitForDb connString

    // Create schema
    printfn "Creating schema..."
    Schema.createSchema connString |> Async.AwaitTask |> Async.RunSynchronously
    printfn "Schema created."
    printfn ""

    // Import data
    Import.importDirectory connString dataDir |> Async.AwaitTask |> Async.RunSynchronously
    0
