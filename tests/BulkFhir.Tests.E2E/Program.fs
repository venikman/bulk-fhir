open Expecto
open BulkFhir.Tests.Fixtures

[<EntryPoint>]
let main args =
    // Start fixture (Testcontainers + TestServer)
    let fixture = TestFixture()
    fixture.StartAsync() |> Async.AwaitTask |> Async.RunSynchronously

    try
        let allTests =
            testList "BulkFhir E2E" [
                BulkFhir.Tests.HealthTests.tests fixture
                BulkFhir.Tests.MetadataTests.tests fixture
                BulkFhir.Tests.GroupTests.tests fixture
                BulkFhir.Tests.ResourceTests.tests fixture
                BulkFhir.Tests.BulkExportTests.tests fixture
            ]

        runTestsWithCLIArgs [] args allTests
    finally
        fixture.StopAsync() |> Async.AwaitTask |> Async.RunSynchronously
