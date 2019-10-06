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
    
    let handler = {
        notificationHandlerStub with 
            SessionStartNotification = notificatorEmptyFunc
            SessionCloseNotification = notificatorEmptyFunc
            EndGameNotification = notificatorEmptyFunc
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
    do! Task.WhenAll waitList |> Task.WhenAll :> Task
}

[<Fact(Skip="eq")>]
//[<Fact>]
let ``process pgn files on session and check correctness`` () = task {
//    let createConnection notificationHandler = task {
       //        let conn = new ServerConnection(sprintf "http://localhost:%d/command" 1313, notificationHandler)
       //        do! conn.Connect()
       //        return conn
       //    }

    let createConnection = createServer()
    let games = getPgnMoves 699 |> Seq.toList |> List.mapi (fun i x -> i, x)
    do! taskThrottle 16 (fun (x, i) -> processGame createConnection (x, i)) games
}