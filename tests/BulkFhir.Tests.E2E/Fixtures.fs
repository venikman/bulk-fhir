module BulkFhir.Tests.Fixtures

open System
open System.Net.Http
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.TestHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Configuration
open Testcontainers.PostgreSql
open Falco
open Falco.Routing
open BulkFhir.Storage
open BulkFhir.Api

let private seedTestData (connString: string) =
    task {
        use conn = new Npgsql.NpgsqlConnection(connString)
        do! conn.OpenAsync()

        // Seed a Patient
        let patientJson = """{"resourceType":"Patient","id":"test-patient-1","name":[{"family":"Smith","given":["John"]}],"gender":"male","birthDate":"1990-05-15"}"""
        use cmd1 = new Npgsql.NpgsqlCommand(
            "INSERT INTO patients (id, family_name, given_name, birth_date, gender, resource_text) VALUES (@id, @fn, @gn, @bd, @g, @rt) ON CONFLICT DO NOTHING", conn)
        cmd1.Parameters.AddWithValue("id", "test-patient-1") |> ignore
        cmd1.Parameters.AddWithValue("fn", "Smith") |> ignore
        cmd1.Parameters.AddWithValue("gn", "John") |> ignore
        cmd1.Parameters.AddWithValue("bd", DateTime(1990, 5, 15)) |> ignore
        cmd1.Parameters.AddWithValue("g", "male") |> ignore
        cmd1.Parameters.AddWithValue("rt", patientJson) |> ignore
        let! _ = cmd1.ExecuteNonQueryAsync()

        // Seed a second Patient
        let patient2Json = """{"resourceType":"Patient","id":"test-patient-2","name":[{"family":"Doe","given":["Jane"]}],"gender":"female","birthDate":"1985-03-20"}"""
        use cmd2 = new Npgsql.NpgsqlCommand(
            "INSERT INTO patients (id, family_name, given_name, birth_date, gender, resource_text) VALUES (@id, @fn, @gn, @bd, @g, @rt) ON CONFLICT DO NOTHING", conn)
        cmd2.Parameters.AddWithValue("id", "test-patient-2") |> ignore
        cmd2.Parameters.AddWithValue("fn", "Doe") |> ignore
        cmd2.Parameters.AddWithValue("gn", "Jane") |> ignore
        cmd2.Parameters.AddWithValue("bd", DateTime(1985, 3, 20)) |> ignore
        cmd2.Parameters.AddWithValue("g", "female") |> ignore
        cmd2.Parameters.AddWithValue("rt", patient2Json) |> ignore
        let! _ = cmd2.ExecuteNonQueryAsync()

        // Seed a Group referencing both patients
        let groupJson = """{"resourceType":"Group","id":"test-group-1","type":"person","actual":true,"name":"Test Attribution Group","quantity":2,"member":[{"entity":{"reference":"Patient/test-patient-1"}},{"entity":{"reference":"Patient/test-patient-2"}}],"identifier":[{"system":"http://example.org","value":"grp-001"}]}"""
        use cmd3 = new Npgsql.NpgsqlCommand(
            "INSERT INTO groups (id, name, identifier_system, identifier_value, resource_text) VALUES (@id, @n, @is, @iv, @rt) ON CONFLICT DO NOTHING", conn)
        cmd3.Parameters.AddWithValue("id", "test-group-1") |> ignore
        cmd3.Parameters.AddWithValue("n", "Test Attribution Group") |> ignore
        cmd3.Parameters.AddWithValue("is", "http://example.org") |> ignore
        cmd3.Parameters.AddWithValue("iv", "grp-001") |> ignore
        cmd3.Parameters.AddWithValue("rt", groupJson) |> ignore
        let! _ = cmd3.ExecuteNonQueryAsync()

        // Seed a Condition
        let conditionJson = """{"resourceType":"Condition","id":"test-condition-1","clinicalStatus":{"coding":[{"system":"http://terminology.hl7.org/CodeSystem/condition-clinical","code":"active"}]},"code":{"coding":[{"system":"http://snomed.info/sct","code":"44054006","display":"Diabetes mellitus type 2"}]},"subject":{"reference":"Patient/test-patient-1"},"onsetDateTime":"2020-01-15"}"""
        use cmd4 = new Npgsql.NpgsqlCommand(
            "INSERT INTO conditions (id, subject_ref, code_system, code_value, clinical_status, category_code, onset_date, resource_text) VALUES (@id, @sr, @cs, @cv, @cls, @cat, @od, @rt) ON CONFLICT DO NOTHING", conn)
        cmd4.Parameters.AddWithValue("id", "test-condition-1") |> ignore
        cmd4.Parameters.AddWithValue("sr", "Patient/test-patient-1") |> ignore
        cmd4.Parameters.AddWithValue("cs", "http://snomed.info/sct") |> ignore
        cmd4.Parameters.AddWithValue("cv", "44054006") |> ignore
        cmd4.Parameters.AddWithValue("cls", "active") |> ignore
        cmd4.Parameters.AddWithValue("cat", DBNull.Value) |> ignore
        cmd4.Parameters.AddWithValue("od", DateTime(2020, 1, 15)) |> ignore
        cmd4.Parameters.AddWithValue("rt", conditionJson) |> ignore
        let! _ = cmd4.ExecuteNonQueryAsync()

        // Seed an Organization
        let orgJson = """{"resourceType":"Organization","id":"test-org-1","name":"Test Hospital","active":true}"""
        use cmd5 = new Npgsql.NpgsqlCommand(
            "INSERT INTO organizations (id, name, resource_text) VALUES (@id, @n, @rt) ON CONFLICT DO NOTHING", conn)
        cmd5.Parameters.AddWithValue("id", "test-org-1") |> ignore
        cmd5.Parameters.AddWithValue("n", "Test Hospital") |> ignore
        cmd5.Parameters.AddWithValue("rt", orgJson) |> ignore
        let! _ = cmd5.ExecuteNonQueryAsync()

        return ()
    }

type TestFixture() =
    let mutable container: PostgreSqlContainer option = None
    let mutable server: TestServer option = None
    let mutable client: HttpClient option = None
    let mutable connStr: string = null

    member _.ConnString = connStr
    member _.Client = client.Value
    member _.Server = server.Value

    member _.StartAsync() =
        task {
            let c =
                PostgreSqlBuilder()
                    .WithDatabase("bulk_fhir_test")
                    .WithUsername("test")
                    .WithPassword("test")
                    .Build()

            do! c.StartAsync()
            container <- Some c
            connStr <- c.GetConnectionString()

            do! Schema.createSchema connStr
            do! seedTestData connStr

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
                            dict [ "ConnectionStrings:DefaultConnection", connStr ]) |> ignore)
                    .ConfigureServices(fun services ->
                        services.AddRouting() |> ignore)
                    .Configure(fun app ->
                        app.UseRouting() |> ignore
                        app.UseFalco(endpoints) |> ignore)

            let s = new TestServer(builder)
            server <- Some s
            client <- Some (s.CreateClient())
        }

    member _.StopAsync() =
        task {
            client |> Option.iter (fun c -> c.Dispose())
            server |> Option.iter (fun s -> s.Dispose())
            match container with
            | Some c -> do! c.DisposeAsync()
            | None -> ()
        }
