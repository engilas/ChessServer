module PgnIntegrationTest

open System
open Xunit
open PgnParser
open ClientBase
open SessionBase
open Types.Command
open FsUnit.Xunit
open Types.Domain
open StateContainer
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2
open ChessConnection

let processGame (createConnection: NotificationHandler -> Task<ServerConnection>) ct game  = task {
    let stateContainer = createStateHistoryContainer()
    
    let handler = {
        notificationHandlerStub with 
            SessionStartNotification = notificatorEmptyFunc
            EndGameNotification = notificatorEmptyFunc
            MoveNotification = stateContainer.PushState
    }
    
    let matchOptions = { Group = Some <| Guid.NewGuid().ToString() }

    let! whiteConn = createConnection handler
    let! blackConn = createConnection handler
        
    do! whiteConn.Match matchOptions ct
    do! blackConn.Match matchOptions ct

    let processMove (connection: ServerConnection) pgnMove = task {
        let moveTask = stateContainer.WaitState (fun _ -> true)

        let primary = pgnMove.Primary
        let move = {
            Src = primary.Src
            Dst = primary.Dst
            PawnPromotion = pgnMove.PawnPromotion
        }

        do! connection.Move move ct
        let! notify = moveTask

        notify |> should equal pgnMove
    }

    for row in game |> Seq.take 1 do
        do! processMove whiteConn row.WhiteMove
        match row.BlackMove with
        | Some move -> 
            do! processMove blackConn move
        | None -> ()

    do! whiteConn.Close()
    do! blackConn.Close()
}

//[<Fact(Skip="eq")>]
[<Fact>]
let ``process pgn files on session and check correctness`` () = task {
    let url = Uri(sprintf "ws://localhost:%d/ws" 1313) 
    let cts = new CancellationTokenSource()
    
    let createConnection notificationHandler = task {
        let conn = new ServerConnection(url, (fun _ -> cts.Cancel(); async.Return ()), (fun () -> async.Return ()), notificationHandler)
        do! conn.Connect()
        conn.Start() |> ignore
        return conn
    }


    let cts = new CancellationTokenSource()
    //let createConnection = createServer cts

    let moves = getPgnMoves 1 |> Seq.toList
    //let tasks = 
    //moves 
    //|> List.map (processGame createConnection cts.Token)
    //|> Task.WhenAll
    //|> ignore
    //let! _ = Task.WhenAll(tasks)
    for move in moves do
        do! processGame createConnection cts.Token move

    ()
}