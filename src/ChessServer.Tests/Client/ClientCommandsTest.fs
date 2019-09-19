module ClientCommandsTest

open Xunit
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.AspNetCore.TestHost

let webAppFactory() = new TestServer(ChessServer.App.createWebHostBuilder([| |]))

[<Fact>]
let ``test ping command``() = async {
    let client = webAppFactory().CreateClient()

    let! r = client.GetAsync("") |> Async.AwaitTask
    0
}
