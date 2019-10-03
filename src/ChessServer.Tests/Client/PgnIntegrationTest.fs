module PgnIntegrationTest
//
//open System
//open Xunit
//open PgnParser
//open ClientBase
//open SessionBase
//open Types.Command
//open FsUnit.Xunit
//open Types.Domain
//open StateContainer
//open System.Threading
//open System.Threading.Tasks
//open FSharp.Control.Tasks.V2
//open ChessConnection
//
//let processGame (createConnection: NotificationHandler -> Task<ServerConnection>) ct (i, game)  = task {
//    let eqwaes  = i
//    let stateContainer = createStateHistoryContainer()
//    
//    let handler = {
//        notificationHandlerStub with 
//            SessionStartNotification = notificatorEmptyFunc
//            SessionCloseNotification = notificatorEmptyFunc
//            EndGameNotification = notificatorEmptyFunc
//            MoveNotification = stateContainer.PushState
//    }
//    
//    let matchOptions = { Group = Some <| Guid.NewGuid().ToString() }
//
//    let! whiteConn = createConnection handler
//    let! blackConn = createConnection handler
//        
//    do! whiteConn.Match matchOptions ct
//    do! blackConn.Match matchOptions ct
//
//    let processMove (connection: ServerConnection) pgnMove = task {
//        let moveTask = stateContainer.WaitState (fun _ -> true)
//
//        let primary = pgnMove.Primary
//        let move = {
//            Src = primary.Src
//            Dst = primary.Dst
//            PawnPromotion = pgnMove.PawnPromotion
//        }
//
//        do! connection.Move move ct
//        let! notify = moveTask
//
//        notify |> should equal pgnMove
//        
//        //do! Async.Sleep 1000
//    }
//
//    for row in game do
//        do! processMove whiteConn row.WhiteMove
//        match row.BlackMove with
//        | Some move -> 
//            do! processMove blackConn move
//        | None -> ()
//
//    do! whiteConn.Close()
//    do! blackConn.Close()
//}
//
//let rec taskThrottle f tasks count = async {
//    match tasks with
//    | _ :: _ ->
//        let taskCount = tasks |> List.length
//        let takeCount = min count taskCount
//        let taken = tasks |> List.take takeCount
//        let left = tasks |> List.skip takeCount
//        try 
//            let! _ =
//                taken |> List.map (f >> Async.AwaitTask)
//                |> Async.Parallel
//            ()
//        with e ->
//            //reraise()
//            ()
//        return! taskThrottle f left count
//    | _ -> ()
//}
//
////[<Fact(Skip="eq")>]
//[<Fact>]
//let ``process pgn files on session and check correctness`` () = task {
//    let url = Uri(sprintf "ws://localhost:%d/ws" 1313) 
//    let cts = new CancellationTokenSource()
//    
//    let errorAction = Action<exn>(fun e ->
//        cts.Cancel()
//    )
//    
//    let createConnection notificationHandler = task {
//        let conn = new ServerConnection(url, errorAction, (fun () -> ()), notificationHandler)
//        do! conn.Connect()
//        return conn
//    }
//
//
////    let createConnection = createServer cts
//
////    let rec tasksLoop games = seq {
////        match games with
////        | game :: games ->
////            
////    }
//    let games = getPgnMoves 100 |> Seq.toList |> List.mapi (fun i x -> i, x)
//    do! taskThrottle (fun (x,i) -> processGame createConnection cts.Token (x, i)) games 8
////    Parallel.ForEach(games, (fun game ->
////        (processGame createConnection cts.Token game).Wait() 
////    )) |> ignore
//
////    let games = getPgnMoves 100 |> Seq.toList
////    let! y =
////        games 
////        |> List.map (processGame createConnection cts.Token >> Async.AwaitTask)
////        |> Async.Parallel
//    
//    //let! _ = Task.WhenAll(tasks)
//    
////    for game in games do
////        do! processGame createConnection cts.Token game
//
//    if cts.IsCancellationRequested then
//        failTest "something goes wrong - cts cancelled"
//}