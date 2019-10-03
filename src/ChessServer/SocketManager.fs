module SocketManager
//
//open System
//open System.Net.WebSockets
//open System.Threading
//open Microsoft.Extensions.Logging
//open Types.Command
//open Types.Channel
//open CommandProcessor
//open StateContainer
//
//[<AutoOpen>]
//module private Internal =
//    open MatchManager
//
//    let logger = Logging.getLogger "SocketManager"
//
//    //todo make periodical ping later
//
//    //let rec startNotificator sendNotify (ct: CancellationToken) = async {
//    //    do sendNotify (TestNotify {Message = "test"})
//    //    do! Async.Sleep 100000
//
//    //    if not ct.IsCancellationRequested then 
//    //        return! startNotificator sendNotify ct
//    //}
//
//    let matcher = createMatcher()
//    
//let processConnection (socket: WebSocket) connectionId =
//
//    use writeAgent = MailboxProcessor.Start(fun inbox ->
//        let rec messageLoop() = async {
//            let! msg = inbox.Receive()
//            do! Socket.write socket msg |> Async.AwaitTask
//            return! messageLoop()  
//        }
//        messageLoop()
//    )
//
//    let writeSocket (msg: string) = 
//        logger.LogInformation("Write message {s}", msg)
//        printfn "%s" msg
//        writeAgent.Post msg
//
//    let ctsNotificator = new CancellationTokenSource()
//
//    let pushMessage serialize obj =
//        let json = serialize obj
//        writeSocket json
//
//    let pushNotify = pushMessage Serializer.serializeNotify
//    let pushResponse id = pushMessage (Serializer.serializeResponse id)
////
////    //startNotificator pushNotify ctsNotificator.Token 
////    //|> Async.Start
////
//    let stateContainer = createStateContainer New
//    
//    let channel = {
//        Id = connectionId
//        PushNotification = pushNotify
//        ChangeState =
//            fun newState ->
//                logger.LogInformation("Channel {0} changing state to {1}", connectionId, newState)
//                stateContainer.SetState newState
//        GetState = stateContainer.GetState
//    }
//    
//    let processCommand = processCommand matcher channel
//    
//    let processSocketMsg msg =
//        printfn "%s" msg // todo replace to logger
//        let {MessageId = id; Request = request} = Serializer.deserializeClientMessage msg
//        let response = processCommand request
//        pushResponse id response
//    
//    let errorAction (e: exn) = 
//        logger.LogError(e, "Error in read loop")
//        pushResponse String.Empty (ErrorResponse InternalErrorResponse)
//        
//    let closeConnection() =
//        processCommand DisconnectCommand |> ignore
//        ctsNotificator.Cancel()
//    
//    Socket.startReader socket processSocketMsg errorAction closeConnection
//
//
