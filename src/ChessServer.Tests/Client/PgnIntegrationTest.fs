module ChessServer.Tests.Client.PgnIntegrationTest

open System
open ChessServer
open ChessServer.Client
open Xunit
open ChessServer.Tests
open PgnParser
open ClientBase
open SessionBase
open ChessServer.Common
open Types.Command
open FsUnit.Xunit
open Types.Domain
open StateContainer
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2
open ChessConnection
open ilf.pgn.Data
open Common.ChessHelper

let processGame (createConnection: NotificationHandler -> Task<ServerConnection>) (i, (game: PgnMove list)) = task {
    let stateContainer = createStateHistoryContainer()
    let endGameContainer = createStateHistoryContainer()
    
    let handler = {
        notificationHandlerStub with 
            SessionStartNotification = notificatorEmptyFunc
            SessionCloseNotification = notificatorEmptyFunc
            EndGameNotification = endGameContainer.PushState
            MoveNotification = stateContainer.PushState
    }
    
    let matchOptions = { Group = Some <| Guid.NewGuid().ToString() }

    use! whiteConn = createConnection handler
    use! blackConn = createConnection handler
        
    do! whiteConn.Match matchOptions |> checkOkResult
    do! blackConn.Match matchOptions |> checkOkResult

    let processMove (connection: ServerConnection) (move: IlfMove) = task {
        //let kl = move.TargetSquare.ToString()
        //let k = getColumn kl
        //let kli = getRow kl
        let parserdMove = moveAction (move.TargetSquare.ToString() |> positionFromString) (move.TargetSquare.ToString() |> positionFromString)
        //let domainMove = {
        //    Src = move.tar
        //}

        let moveTask = stateContainer.WaitState (fun x ->
            x.Primary.Src = parserdMove.Src && x.Primary.Dst = parserdMove.Dst
        )
        
        let promoted = 
            if move.PromotedPiece.HasValue then
                match move.PromotedPiece.Value with
                | PieceType.Bishop -> Some Bishop
                | PieceType.King -> Some King
                | PieceType.Queen -> Some Queen
                | PieceType.Rook -> Some Rook
                | PieceType.Knight -> Some Knight
                | PieceType.Pawn -> Some Pawn
                | _ -> failwithf "unknown type %A" move.PromotedPiece.Value
            else None
        //move.OriginSquare.ToString() |> positionFromString

        let moveCommand : MoveCommand = {
            Move = parserdMove
            PawnPromotion = promoted
        }

        do! connection.Move moveCommand |> checkOkResult
        
        let! notify = moveTask
        notify.Primary |> should equal parserdMove
        notify.Check |> should equal move.IsCheck
        notify.Mate |> should equal move.IsCheckMate
        notify.PawnPromotion |> should equal promoted
    }
    
    for row in game do
        let color, move = row
        let connection = 
            match color with
            | White -> whiteConn
            | Black -> blackConn

        do! processMove connection move
        
    let checkEndGame color = task {
        // wait events
        let rec waitSecond cnt = task {
            if cnt < 5 then
                match  endGameContainer.GetHistory().Length with
                | 2 -> ()
                | _ -> do! Task.Delay(1000)
                       return! waitSecond (cnt + 1)
        }
        do! waitSecond 0
        
        match endGameContainer.GetHistory() with
        | endGame1 :: endGame2 :: [] when endGame1 = endGame2 ->
            match endGame1.Result with
            | WhiteWin -> color |> should equal White
            | BlackWin -> color |> should equal Black
            | Draw -> failwith "Draw is not supported yet"
        | x -> failwithf "Invalid end game notification %A: i = %d" x i
    }
    
    let _, last = List.last game
    if last.IsCheckMate.HasValue && last.IsCheckMate.Value then do! checkEndGame White
    
    do! whiteConn.Close()
    do! blackConn.Close()
}

let rec taskThrottle maxDegree (f: 'a -> Task<_>) (tasks: 'a list) = task {
    use sem = new SemaphoreSlim(maxDegree)
    let waitList =
        tasks
        |> List.map(fun x -> task {
            do! sem.WaitAsync()
            return task {
                try
                    do! f x
                finally
                    sem.Release() |> ignore
            }
        })
    let! taskList = Task.WhenAll waitList
    let! _ = taskList |> Task.WhenAll
    for task in taskList do
        if not <| task.IsCompletedSuccessfully then failTest "Not all tasks completed successfully"
}

[<Fact(Skip="long")>]
//[<Fact>]
let ``process pgn files on session and check correctness - long`` () = task {
//    let createConnection notificationHandler = task {
       //        let conn = new ServerConnection(sprintf "http://localhost:%d/command" 1313, notificationHandler)
       //        do! conn.Connect()
       //        return conn
       //    }

    use! server = createServer()
    let games = allPgnMoves() |> Seq.toList |> List.mapi (fun i x -> i, x)
    do! taskThrottle 16 (fun (x, i) -> processGame (server.GetClient) (x, i)) games
}

[<Fact>]
let ``process pgn files on session and check correctness - short`` () = task {
    use! server = createServer()
    let games = getPgnMoves 64 |> Seq.toList |> List.mapi (fun i x -> i, x)
    do! taskThrottle 4 (fun (x, i) -> processGame (server.GetClient) (x, i)) games
}