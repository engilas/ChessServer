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

let processGame (createConnection: NotificationHandler -> Task<ServerConnection>) (i, game) = task {
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

    let processMove (connection: ServerConnection) pgnMove = task {
        let primary = pgnMove.Primary
        let moveTask = stateContainer.WaitState (fun x ->
            x.Primary.Src = primary.Src && x.Primary.Dst = primary.Dst
        )

        let move = {
            Src = primary.Src
            Dst = primary.Dst
            PawnPromotion = pgnMove.PawnPromotion
        }

        do! connection.Move move |> checkOkResult
        
        let! notify = moveTask
        notify |> should equal pgnMove
    }
    
    for row in game do
        do! processMove whiteConn row.WhiteMove
        match row.BlackMove with
        | Some move -> 
            do! processMove blackConn move
        | None -> ()
        
    let checkEndGame color = task {
        // wait events
        let rec waitSecond cnt = task {
            if cnt < 10 then
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
        // bug when i = 1824
        | x -> failwithf "Invalid end game notification %A: i = %d" x i
    }
    
    let last = List.last game
    if last.WhiteMove.Mate then do! checkEndGame White
    else
        match last.BlackMove with
        | Some row ->
            if row.Mate then do! checkEndGame Black
        | None -> ()

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

    let createConnection = createServer()
    let games = allPgnMoves() |> Seq.toList |> List.mapi (fun i x -> i, x)
    do! taskThrottle 16 (fun (x, i) -> processGame createConnection (x, i)) games
}

[<Fact>]
let ``process pgn files on session and check correctness - short`` () = task {
    let createConnection = createServer()
    let games = getPgnMoves 64 |> Seq.toList |> List.mapi (fun i x -> i, x)
    do! taskThrottle 4 (fun (x, i) -> processGame createConnection (x, i)) games
}