module ClientCommandsTest

open ChessConnection
open ChessServer
open Xunit
open System
open Microsoft.AspNetCore.Hosting
open Types.Command
open System.Threading
open FSharp.Control.Tasks.V2
open SessionBase
open StateContainer
open Types.Domain
open FsUnit.Xunit

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
    
let notificatorEmptyFunc _ = ()

let notificationHandlerStub = {
    ChatNotification = notificatorErrorFunc
    MoveNotification = notificatorErrorFunc
    EndGameNotification = notificatorErrorFunc
    SessionStartNotification = notificatorErrorFunc
    SessionCloseNotification = notificatorErrorFunc
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

    use! whiteConn = getConnection handler cts
    use! blackConn = getConnection handler cts

    let whiteWait = stateContainer.WaitState (fun {Color = x} -> x = White)
    let blackWait = stateContainer.WaitState (fun {Color = x} -> x = Black)
    
    do! whiteConn.Match cts.Token
    do! blackConn.Match cts.Token
    
    let! _ = whiteWait
    let! _ = blackWait
    
    ()
}

[<Fact>]
let ``test chat command``() = task {
    let stateContainer = createStateContainer ""

    let handler = {
        notificationHandlerStub with 
            ChatNotification = fun msg -> stateContainer.SetState msg
            SessionStartNotification = notificatorEmptyFunc
    }

    let cts = new CancellationTokenSource()
    use! whiteConn = getConnection handler cts
    use! blackConn = getConnection handler cts
    
    do! whiteConn.Match cts.Token
    do! blackConn.Match cts.Token

    let whiteWait = stateContainer.WaitState ((=) "white")
    let blackWait = stateContainer.WaitState ((=) "black")
    
    do! whiteConn.Chat "white" cts.Token
    let! _ = whiteWait

    do! blackConn.Chat "black" cts.Token
    let! _ = blackWait
    
    ()
}

[<Fact>]
let ``test move command``() = task {
    let stateContainer = createStateHistoryContainer()

    let handler = {
        notificationHandlerStub with 
            SessionStartNotification = notificatorEmptyFunc
            MoveNotification = stateContainer.PushState
    }

    let cts = new CancellationTokenSource()
    use! whiteConn = getConnection handler cts
    use! blackConn = getConnection handler cts
    
    do! whiteConn.Match cts.Token
    do! blackConn.Match cts.Token
    
    let moveTask = stateContainer.WaitState (fun _ -> true)
    let move = getMove "a2" "a4"
    do! whiteConn.Move (getMove "a2" "a4") cts.Token
    let! moveNotify = moveTask
    
    moveNotify.Check |> should equal false
    moveNotify.Mate |> should equal false
    moveNotify.Primary.Src |> should equal move.Src
    moveNotify.Primary.Dst |> should equal move.Dst
    moveNotify.Secondary |> should equal None
    moveNotify.PawnPromotion |> should equal None
    moveNotify.TakenPiecePos |> should equal None
}

[<Fact>]
let ``test disconnect command``() = task {
    let stateContainer = createStateContainer 0

    let handler = {
        notificationHandlerStub with 
            SessionStartNotification = notificatorEmptyFunc
            SessionCloseNotification = fun _ -> stateContainer.SetState 1
    }

    let cts = new CancellationTokenSource()
    use! whiteConn = getConnection handler cts
    use! blackConn = getConnection handler cts
    
    do! whiteConn.Match cts.Token
    do! blackConn.Match cts.Token
    
    let waitTask = stateContainer.WaitState ((=) 1)
    do! whiteConn.Disconnect cts.Token
    let! notification = waitTask
    ()
}

// больше асинхронных комманд (?)
//Добавить периодический пинг (чтобы клиент пинговал, сервер отвечал, иначе: если сервер не ответит - дисконнект. Если клиент давно не пинговал - дисконнект)
// добавить восстановление сесии при дисконнекте