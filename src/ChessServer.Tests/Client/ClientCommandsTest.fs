module ClientCommandsTest

open ChessConnection
open ChessServer
open Xunit
open System
open Microsoft.AspNetCore.Hosting
open Types.Command
open System.Threading
open FSharp.Control.Tasks.V2

type PortResourceMessage = AsyncReplyChannel<int>

let portResourceAgent = MailboxProcessor<PortResourceMessage>.Start(fun inbox ->
    let rec loop ports = async {
        let! channel = inbox.Receive()
        match ports with
        | port::ports ->
            channel.Reply port
            return! loop ports
        | [] -> channel.Reply -1
    }
    
    loop [2000..65535]
)

let getConnection (cts: CancellationTokenSource) = task {
    let builder = (App.createWebHostBuilder [||]).UseUrls("http://*:2121")
    let _ = builder.Build().RunAsync()
    let url = Uri("ws://localhost:2121/ws") 
    let conn = new ServerConnection(url, (fun _ -> cts.Cancel(); async.Return ()), (fun () -> async.Return ()))
    do! conn.Connect()
    conn.Start() |> ignore
    return conn
}
    
    

[<Fact>]
let ``test ping command``() = task {
    let cts = new CancellationTokenSource()
    use! conn = getConnection cts
    
    do! conn.Ping "eqe" cts.Token
}

[<Fact>]
let ``test match command``() = task {
    let cts = new CancellationTokenSource()
    use! conn = getConnection cts
    
    do! conn.Match cts.Token
}

[<Fact>]
let ``test chat command``() = task {
    let cts = new CancellationTokenSource()
    use! whiteConn = getConnection cts
    use! blackConn = getConnection cts
    
    do! whiteConn.Match cts.Token
    do! blackConn.Match cts.Token
    //do! whiteConn.Chat "white" cts.Token
    //do! blackConn.Chat "black" cts.Token
}