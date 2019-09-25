﻿module ClientCommandsTest

open ChessConnection
open Xunit
open ClientBase
open Types.Command
open System.Threading
open FSharp.Control.Tasks.V2
open SessionBase
open StateContainer
open Types.Domain
open FsUnit.Xunit
open System.Threading.Tasks

[<Fact>]
let ``test ping command``() = task {
    let cts = new CancellationTokenSource()
    let createConnection = createServer cts
    use! conn = createConnection notificationHandlerStub
    
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

    let createConnection = createServer cts

    use! whiteConn = createConnection handler
    use! blackConn = createConnection handler

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
    let createConnection = createServer cts
    
    use! whiteConn = createConnection handler
    use! blackConn = createConnection handler
    
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
    let createConnection = createServer cts

    use! whiteConn = createConnection handler
    use! blackConn = createConnection handler
    
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
    let createConnection = createServer cts
    
    use! whiteConn = createConnection handler
    use! blackConn = createConnection handler
    
    let waitTask = stateContainer.WaitState ((=) 1)

    do! whiteConn.Match cts.Token
    do! blackConn.Match cts.Token
    
    do! whiteConn.Disconnect cts.Token
    let timeout = Async.Sleep(1000 * 30) |> Async.StartAsTask
    let! notification =  Task.WhenAny(timeout, waitTask)
    if timeout.IsCompletedSuccessfully then failTest "failed by timeout"
    ()
}

// больше асинхронных комманд (?)
//Добавить периодический пинг (чтобы клиент пинговал, сервер отвечал, иначе: если сервер не ответит - дисконнект. Если клиент давно не пинговал - дисконнект)
// добавить восстановление сесии при дисконнекте