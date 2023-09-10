module ChessServer.Tests.Client.ClientCommandsTest

open System
open ChessServer
open ChessServer.Client
open ChessServer.Common
open ChessServer.Tests
open ChessConnection
open Xunit
open ClientBase
open Types.Command
open FSharp.Control.Tasks.V2
open SessionBase
open StateContainer
open Types.Domain
open FsUnit.Xunit
open System.Threading.Tasks

//let createServerArgs = createServer
//let createServer() = createServer None

let getMatchedConnectionsFull (server: TestServer) handler = task {
    let stateContainer = createStateHistoryContainer()
    
    let handler = { handler with SessionStartNotification = fun x -> stateContainer.PushState x }
    
    let! whiteConn = server.GetClient handler
    let! blackConn = server.GetClient handler

    let whiteWait = stateContainer.WaitState (fun {Color = x} -> x = White)
    let blackWait = stateContainer.WaitState (fun {Color = x} -> x = Black)
    
    do! whiteConn.Match defaultMatcherOptions |> checkOkResult
    do! blackConn.Match defaultMatcherOptions |> checkOkResult
    
    let! _ = whiteWait
    let! _ = blackWait
    
    return (fun () -> whiteConn), (fun () -> blackConn)//, createConnection
}

let getMatchedConnections server handler = task {
    let! w, g = getMatchedConnectionsFull server handler
    return w, g
}

//let getMatchedConnections handler = getMatchedConnectionsArgs handler

[<Fact>]
let ``test ping command``() = task {
    use! server = createServer()
    use! conn = server.GetClient notificationHandlerStub
    
    let! response = conn.Ping()
    match response with
    | PingResponse _ -> ()
    | _ -> failTest "invalid response"
}

[<Fact>]
let ``test match command``() = task {
    use! server = createServer()
    let! gw, gb = getMatchedConnections server notificationHandlerStub
    use _ = gw()
    use _ = gb()
    ()
}

[<Fact>]
let ``test chat command``() = task {
    use! server = createServer()
    let stateContainer = createStateContainer ""

    let handler = {
        notificationHandlerStub with 
            ChatNotification = fun msg -> stateContainer.SetState msg
    }
    let! gw, gb = getMatchedConnections server handler
    use whiteConn = gw()
    use blackConn = gb()

    let whiteWait = stateContainer.WaitState ((=) "white")
    let blackWait = stateContainer.WaitState ((=) "black")
    
    do! whiteConn.Chat "white" |> checkOkResult
    let! _ = whiteWait

    do! blackConn.Chat "black" |> checkOkResult
    let! _ = blackWait
    
    ()
}

[<Fact>]
let ``test move command``() = task {
    use! server = createServer()
    let stateContainer = createStateHistoryContainer()

    let handler = {
        notificationHandlerStub with 
            MoveNotification = stateContainer.PushState
    }
    
    let! gw, gb = getMatchedConnections server handler
    use whiteConn = gw()
    use _ = gb()
    
    let moveTask = stateContainer.WaitState (fun _ -> true)
    let move = getMove "a2" "a4"
    do! whiteConn.Move move |> checkOkResult
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
    use! server = createServer()
    let stateContainer = createStateContainer 0

    let handler = {
        notificationHandlerStub with 
            SessionCloseNotification = fun _ -> stateContainer.SetState 1
    }

    let! gw, gb = getMatchedConnections server handler
    use whiteConn = gw()
    use _ = gb()
    
    let waitTask = stateContainer.WaitState ((=) 1)
    
    do! whiteConn.Disconnect() |> checkOkResult
    let! _ = waitTask
    ()
}


[<Fact>]
let ``test close``() = task {
    use! server = createServer()
    use! conn = server.GetClient notificationHandlerStub
    do! conn.Close()
    //do! conn.DisposeAsync()
}

[<Fact>]
let ``test restore - invalid channel``() = task {
    use! server = createServer()
    use! conn = server.GetClient notificationHandlerStub
    let! response =  conn.Restore (ConnectionId "abcd")
    match response with
    | ErrorResponse (ReconnectError msg) ->
        msg |> should haveSubstring "Invalid channel"
    | _ -> failwith "Invalid response"
}

[<Fact>]
let ``test restore - same channel``() = task {
    use! server = createServer()
    use! conn = server.GetClient notificationHandlerStub
    let! response =  conn.Restore(conn.GetConnectionId())
    match response with
    | ErrorResponse (ReconnectError msg) ->
        msg |> should haveSubstring "Invalid channel"
    | _ -> failwith "Invalid response"
}

[<Fact>]
let ``test restore - not new channel``() = task {
    use! server = createServer()
    use! conn = server.GetClient notificationHandlerStub
    use! conn2 = server.GetClient notificationHandlerStub
    let conn2Id = conn2.GetConnectionId()
    //do! conn2.DisposeAsync()
    //conn2.Dispose()
    do! conn2.Close()
    do! conn.Match defaultMatcherOptions |> checkOkResult
    let! response =  conn.Restore(conn2Id)
    match response with
    | ErrorResponse (ReconnectError msg) ->
        msg |> should haveSubstring "new channel"
    | _ -> failwith "Invalid response"
}

[<Fact>]
let ``test restore - active channel``() = task {
    use! server = createServer()
    use! conn = server.GetClient notificationHandlerStub
    use! conn2 = server.GetClient notificationHandlerStub
    let conn2Id = conn2.GetConnectionId()
    let! response = conn.Restore(conn2Id)
    match response with
    | ErrorResponse (ReconnectError msg) ->
        msg |> should haveSubstring "Invalid channel"
    | _ -> failwith "Invalid response"
}

[<Fact>]
let ``test restore``() = task {
    use! server = createServer()
    let! gw, gb = getMatchedConnectionsFull server notificationHandlerStub
    use whiteConn = gw()
    use _ = gb()
    
    let id = whiteConn.GetConnectionId()
    //do! whiteConn.DisposeAsync()
    do! whiteConn.Close()
    let! newWhiteConn = server.GetClient notificationHandlerStub
    do! newWhiteConn.Restore id |> checkOkResult
}

//[<Fact>]
//let ``test disconnect by timeout``() = task {
//    let stateContainer = createStateContainer None
//    let handler = {
//        notificationHandlerStub with 
//            SessionCloseNotification = fun x -> stateContainer.SetState <| Some x
//    }

//    use server = createServer()
    
//    let! gw, gb = getMatchedConnectionsArgs server handler (Some [|"--DisconnectTimeout=1"|])
//    use whiteConn = gw()
//    use _ = gb()
    
//    let start = DateTime.Now
//    let wait = stateContainer.WaitState Option.isSome
//    do! whiteConn.Close()
//    let! closeReason = wait
//    match closeReason with
//    | Some OpponentDisconnected -> ()
//    | _ -> failTest "Invalid close reason"
//    let delta = DateTime.Now - start
//    if delta < TimeSpan.FromSeconds(1.0) then
//        failTest "Disconnect time test failed"
//}

//[<Fact>]
//let ``check close event``() = task {
//    let createConnection = createServer()
//    use! conn = createConnection notificationHandlerStub

//    let stateContainer = createStateContainer 0

//    conn.add_Closed(fun e -> 
//        stateContainer.SetState 1
//        Task.CompletedTask
//    )

//    do! conn.Close()
//    let! _ = stateContainer.WaitState ((=) 1)
//    ()
//}

//[<Fact>]
//let ``check events after restore``() = task {
//    let stateContainer = createStateHistoryContainer()

//    let handler = {
//        notificationHandlerStub with 
//            MoveNotification = stateContainer.PushState
//    }

//    let! gw, gb = getMatchedConnections handler
//    use whiteConn = gw()
//    use blackConn = gb()

//    let reconnState = createStateContainer 0
    
//    whiteConn.add_Reconnected(fun e -> 
//        reconnState.SetState 1
//        Task.CompletedTask
//    )

//    let id = whiteConn.GetConnectionId()
    
//    try
//        do! whiteConn.TestDisconnect()
//    with :? TaskCanceledException ->
//        ()
//    let! _ = reconnState.WaitState ((=) 1)

//    do! whiteConn.Restore(id) |> checkOkResult
//    let move = getMove "a2" "a4"
//    do! whiteConn.Move move |> checkOkResult

//    let! _ = stateContainer.WaitState (fun m -> m.Primary.Src = move.Src)

//    let move = getMove "a7" "a5"
//    do! blackConn.Move move |> checkOkResult

//    let! _ = stateContainer.WaitState (fun m -> m.Primary.Src = move.Src)

//    let moves = stateContainer.GetHistory()

//    ()
//}

//[<Fact>]
//let ``check double restore``() = task {
//    let! gw, gb = getMatchedConnections notificationHandlerStub
//    use whiteConn = gw()
//    use _ = gb()

//    let reconnState = createStateContainer 0
//    whiteConn.add_Reconnected(fun e -> 
//        reconnState.SetState 1
//        Task.CompletedTask
//    )

//    let reconnect() = task {
//        reconnState.SetState 0

//        let id = whiteConn.GetConnectionId()
//        try
//            do! whiteConn.TestDisconnect()
//        with :? TaskCanceledException ->
//            ()
//        let! _ = reconnState.WaitState ((=) 1)

//        do! whiteConn.Restore(id) |> checkOkResult
//    }

//    do! reconnect()
//    do! reconnect()
//}

// больше асинхронных комманд (?)
// Добавить периодический пинг (чтобы клиент пинговал, сервер отвечал, иначе: если сервер не ответит - дисконнект. Если клиент давно не пинговал - дисконнект)
// добавить предложение о ничье