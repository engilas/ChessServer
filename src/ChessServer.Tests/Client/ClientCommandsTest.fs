module ClientCommandsTest

open ChessConnection
open ChessServer
open Xunit
open System
open Microsoft.AspNetCore.Hosting
open Types.Command
open System.Threading
open FSharp.Control.Tasks.V2
open StateContainer
open Types.Domain

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

let notificatorErrorFunc _ = 
    failwith "invalid notification"

let notificationHandlerStub = {
    ChatNotification = notificatorErrorFunc
    MoveNotification = notificatorErrorFunc
    EndGameNotification = notificatorErrorFunc
    SessionStartNotification = notificatorErrorFunc
    SessionCloseNotify = notificatorErrorFunc
}

let getConnection notificationHandler (cts: CancellationTokenSource) = task {
    let port = portResourceAgent.PostAndReply id
    let builder = (App.createWebHostBuilder [||]).UseUrls(sprintf "http://*:%d" port)
    let _ = builder.Build().RunAsync()
    let url = Uri(sprintf "ws://localhost:%d/ws" port) 
    let conn = new ServerConnection(url, (fun _ -> cts.Cancel(); async.Return ()), (fun () -> async.Return ()), notificationHandler)
    do! conn.Connect()
    conn.Start() |> ignore
    return conn
}
    
    

[<Fact>]
let ``test ping command``() = task {
    let cts = new CancellationTokenSource()
    use! conn = getConnection notificationHandlerStub cts
    
    do! conn.Ping "eqe" cts.Token
}

[<Fact>]
let ``test match command``() = task {
    let stateContainer = createStateHistoryContainer()

    let handler = {
        notificationHandlerStub with 
            SessionStartNotification = fun x -> stateContainer.PushState x
    }

    let cts = new CancellationTokenSource()
    //use! conn = getConnection notificationHandlerStub cts

    use! whiteConn = getConnection handler cts
    use! blackConn = getConnection handler cts
    
    do! whiteConn.Match cts.Token
    do! blackConn.Match cts.Token

    //todo уведомления приходят не сразу!
    match stateContainer.GetHistory() with
    | black :: white :: [] when white.Color = White && black.Color = Black -> ()
    | q -> failwith "Invalid notification"
        
}

[<Fact>]
let ``test chat command``() = task {
    let stateContainer = createStateContainer ""

    let checkChatMsg msg = 
        match stateContainer.GetState() with
        | x when x = msg -> ()
        | _ -> failwith "Check chat msg failed"

    let handler = {
        notificationHandlerStub with 
            ChatNotification = fun msg -> stateContainer.SetState msg
            SessionStartNotification = fun _ -> ()
    }

    handler.SessionStartNotification {Color = Black}

    let cts = new CancellationTokenSource()
    use! whiteConn = getConnection handler cts
    use! blackConn = getConnection handler cts
    
    do! whiteConn.Match cts.Token
    do! blackConn.Match cts.Token

    do! whiteConn.Chat "white" cts.Token
    checkChatMsg "white"

    do! blackConn.Chat "black" cts.Token
    checkChatMsg "black"
}