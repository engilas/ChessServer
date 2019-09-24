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
    
    //todo wait notify here

    do! whiteConn.Match cts.Token
    do! blackConn.Match cts.Token

    //todo уведомления приходят не сразу!
    match stateContainer.GetHistory() with
    | p1 :: p2 :: [] 
        when 
        [ p1 ; p2 ]
        |> (fun lst -> 
            lst |> List.exists (fun x -> x.Color = White) 
            && lst |> List.exists (fun x -> x.Color = Black)
        ) -> ()
    | x -> failwithf "Invalid notification %A" x
        
}

[<Fact>]
let ``test chat command``() = task {
    let stateContainer = createStateContainer ""

    let checkChatMsg msg = 
        match stateContainer.GetState() with
        | x when x = msg -> ()
        | x -> failwithf "Check chat msg failed %A" x

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

    let waitTask = stateContainer.WaitState ((=) "white")
    do! whiteConn.Chat "white" cts.Token
    let! _ = waitTask
    checkChatMsg "white"

    let waitTask = stateContainer.WaitState ((=) "black")
    do! blackConn.Chat "black" cts.Token
    let! _ = waitTask
    checkChatMsg "black"
}

//todo Добавить ждалки стейтов. 
// больше асинхронных комманд (?)
//Добавить периодический пинг (чтобы клиент пинговал, сервер отвечал, иначе: если сервер не ответит - дисконнект. Если клиент давно не пинговал - дисконнект)
// добавить восстановление сесии при дисконнекте