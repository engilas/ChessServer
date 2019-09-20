module ClientCommandsTest

open ChessServer
open Xunit
open Microsoft.AspNetCore.TestHost
open System
open Microsoft.AspNetCore.Hosting
open Types.Command
open System.Threading

[<Fact>]
let ``serialize test``() = 
    let response = PingResponse {Message = "qeq"}
    let ser = Serializer.serializeResponse "" response

    0

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

    let cts = new CancellationTokenSource()
    let ct = cts.Token
    
    let processError (e: exn) = cts.Cancel(); async.Return ()

    let url = Uri("ws://localhost:2121/ws") 
    let! conn = ChessConnection.createConnection url processError (fun () -> async.Return ())
    
    let readerTask = conn.Start() |> Async.StartAsTask
    
    let! response = conn.Ping {Message= "eqe"} ct
    
    return ()
}
