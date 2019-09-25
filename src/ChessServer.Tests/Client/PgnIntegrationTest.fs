module PgnIntegrationTest

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
            MoveNotification = stateContainer.PushState
    }

    use! whiteConn = createConnection handler
    use! blackConn = createConnection handler
        
    do! whiteConn.Match ct
    do! blackConn.Match ct

    let processMove (connection: ServerConnection) pgnMove = task {
        let primary = pgnMove.Primary
        let move = {
            Src = primary.Src
            Dst = primary.Dst
            PawnPromotion = pgnMove.PawnPromotion
        }

        let moveTask = stateContainer.WaitState (fun _ -> true)
        do! connection.Move move ct
        let! notify = moveTask

        notify |> should equal pgnMove
    }

    for row in game do
        do! processMove whiteConn row.WhiteMove
        match row.BlackMove with
        | Some move -> 
            do! processMove blackConn move
        | None -> ()
}

//[<Fact(Skip="eq")>]
[<Fact>]
let ``process pgn files on session and check correctness`` () = task {
    let cts = new CancellationTokenSource()
    let createConnection = createServer cts

    let moves = getPgnMoves 1 |> Seq.toList
    let tasks = moves |> List.map (processGame createConnection cts.Token)
    let! _ = System.Threading.Tasks.Task.WhenAll(tasks)
    ()
}