module ClientBase

open System.Threading
open FSharp.Control.Tasks.V2
open ChessServer
open Microsoft.AspNetCore.Hosting
open System
open ChessConnection

type PortResourceMessage = AsyncReplyChannel<int>

let portResourceAgent = MailboxProcessor<PortResourceMessage>.Start(fun inbox ->
    let rec loop port = async {
        let! channel = inbox.Receive()
        channel.Reply port
        let nextPort = 
            match port with
            | 65535 -> 2000
            | x -> x + 1
        return! loop nextPort
    }
    
    loop 2000
)

let createServer (cts: CancellationTokenSource) = 
    let port = portResourceAgent.PostAndReply id
    let builder = (App.createWebHostBuilder [||]).UseUrls(sprintf "http://*:%d" port)
    let _ = builder.Build().RunAsync()
    let url = Uri(sprintf "ws://localhost:%d/ws" port)
    
    let errorAction e =
        cts.Cancel()

    fun notificationHandler -> task {
        let conn = new ServerConnection(url, errorAction, (fun () -> ()), notificationHandler)
        do! conn.Connect()
        return conn
    }