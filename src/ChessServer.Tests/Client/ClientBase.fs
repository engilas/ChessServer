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

let createServer() = 
    let port = portResourceAgent.PostAndReply id
    let builder = (App.createWebHostBuilder [||]).UseUrls(sprintf "http://*:%d" port)
    let _ = builder.Build().RunAsync()
    let url = sprintf "http://localhost:%d/command" port

    fun notificationHandler -> task {
        let conn = new ServerConnection(url, notificationHandler)
        do! conn.Connect()
        return conn
    }
    
let checkOkResult (x: Task<_>) = task {
    let! result = x
    match result with
    | OkResponse -> ()
    | x -> failwithf "invalid response %A" x
}