module BulkFhir.Tests.DashboardSchemaTests

open System
open Expecto
open Swensen.Unquote
open Npgsql

let private getConnectionString () =
    Environment.GetEnvironmentVariable("CONNECTION_STRING")
    |> Option.ofObj
    |> Option.defaultWith (fun () -> failwith "CONNECTION_STRING is required for dashboard schema tests.")

let private querySingle<'T> (sql: string) =
    task {
        use conn = new NpgsqlConnection(getConnectionString())
        do! conn.OpenAsync()
        use cmd = new NpgsqlCommand(sql, conn)
        let! value = cmd.ExecuteScalarAsync()
        return unbox<'T> value
    }

let tests =
    testList "Dashboard schema" [
        testTask "dashboard SQL views exist" {
            let! count =
                querySingle<int>
                    "SELECT count(*)::int FROM information_schema.views WHERE table_schema = 'public' AND table_name IN ('dashboard_overview_v','dashboard_resource_coverage_v','dashboard_group_summary_v','dashboard_observation_codes_v','dashboard_quality_v','dashboard_export_jobs_v')"
            test <@ count = 6 @>
        }

        testTask "dashboard overview view returns a row" {
            let! count = querySingle<int> "SELECT count(*)::int FROM dashboard_overview_v"
            test <@ count >= 1 @>
        }

        testTask "dashboard group summary exposes quantity derived from resource JSON" {
            let! count =
                querySingle<int>
                    "SELECT count(*)::int FROM dashboard_group_summary_v WHERE quantity IS NOT NULL"
            test <@ count >= 1 @>
        }
    ]
