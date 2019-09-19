module ClientCommandsTest

open ChessServer
open Xunit
open Microsoft.AspNetCore.Mvc.Testing
open Microsoft.AspNetCore.TestHost
open System
open System.IO
open System.Net.Http
open System.Threading
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Cors.Infrastructure
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.Extensions.Configuration

let webAppFactory() =
//    let builder =
//        WebHostBuilder()
//            .UseKestrel()
//            .Configure(Action<IApplicationBuilder> App.configureApp)
//            .ConfigureServices(App.configureServices)
    new TestServer(App.createWebHostBuilder [||])

[<Fact>]
let ``test ping command``() = async {
    let builder = (App.createWebHostBuilder [||]).UseUrls("http://*:2121")
    let builderTask = builder.Build().RunAsync()
    
    let url = Uri("ws://localhost:2121/ws") 
    let! conn = ChessConnection.createConnection url 
    
    let readerTask = conn.Start() |> Async.StartAsTask
    
    let! response = conn.Ping {Message= "eqe"}
    
    0
}
