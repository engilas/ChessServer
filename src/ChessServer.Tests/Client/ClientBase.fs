module ChessServer.Tests.Client.ClientBase

open System.Threading
open FSharp.Control.Tasks.V2
open ChessServer
open ChessServer.Client
open ChessServer.Common
open Microsoft.AspNetCore.Hosting
open System.Threading.Tasks
open ChessConnection
open Types.Command
open Suave
open ChessServer.Network.Server
open Suave.Logging
open System

type PortResourceMessage = AsyncReplyChannel<int>

type TestServer(getClient: NotificationHandler -> Task<ServerConnection>, cts: CancellationTokenSource) =
    interface IDisposable with
        member this.Dispose() = cts.Cancel()

    member this.GetClient(handler: NotificationHandler) = getClient handler



let portResourceAgent = MailboxProcessor<PortResourceMessage>.Start(fun inbox ->
    let min = 23182
    let max = 65535

    let rec loop port = async {
        let! channel = inbox.Receive()
        channel.Reply port
        let nextPort = 
            match port with
            | x when x = max -> min
            | x -> x + 1
        return! loop nextPort
    }
    
    loop min
)

let createServer() = async {
        let port = portResourceAgent.PostAndReply id
        let cts = new CancellationTokenSource()
        let listening, server = 
            startWebServerAsync {
                defaultConfig with logger = Targets.create Verbose [||]; 
                                   bindings = [ HttpBinding.createSimple HTTP "127.0.0.1" port ] 
                                   cancellationToken = cts.Token
            } app
        Async.Start(server, cts.Token)
        let! _ = listening
        //let builder = (App.createWebHostBuilder <| Option.defaultValue [||] args).UseUrls(sprintf "http://*:%d" port)
        //let _ = builder.Build().RunAsync()
        let url = sprintf "ws://127.0.0.1:%d/ws" port

        return new TestServer(fun notificationHandler -> task {
            let conn = new ServerConnection(url, notificationHandler)
            do! conn.Connect()
            return conn
        }, cts)
    }
    
let checkOkResult (x: Task<_>) = task {
    let! result = x
    match result with
    | OkResponse -> ()
    | x -> failwithf "invalid response %A" x
}