module SocketManager

open System
open System.Net.WebSockets
open System.Threading
open System.Text
open Microsoft.Extensions.Logging
open Types.Command
open Types.Channel
open CommandProcessor
open StateContainer

[<AutoOpen>]
module private Internal =
    open MatchManager

    let logger = Logging.getLogger "SocketManager"

    let rec startNotificator sendNotify (ct: CancellationToken) = async {
        do sendNotify (TestNotify {Message = "test"})
        do! Async.Sleep 100000

        if not ct.IsCancellationRequested then 
            return! startNotificator sendNotify ct
    }

    let matcher = createMatcher()
    
let processConnection (connection: WebSocket) connectionId = async {

    use writeAgent = MailboxProcessor.Start(fun inbox ->
        let rec messageLoop() = async {
            let! msg = inbox.Receive()
            do! Socket.write connection msg CancellationToken.None
            return! messageLoop()  
        }
        messageLoop()
    )

    let writeSocket (msg: string) = 
        logger.LogInformation("Write message {s}", msg)
        writeAgent.Post msg

    let ctsNotificator = new CancellationTokenSource()

    let pushMessage serialize obj =
        let json = serialize obj
        writeSocket json

    let pushNotify = pushMessage Serializer.serializeNotify
    let pushResponse id = pushMessage (Serializer.serializeResponse id)

    startNotificator pushNotify ctsNotificator.Token 
    |> Async.Start

    let stateContainer = createStateContainer New

    let channel = {
        Id = connectionId
        PushNotification = pushNotify
        ChangeState =
            fun newState -> 
                logger.LogInformation("Channel {0} changing state to {1}", connectionId, newState)
                stateContainer.SetState newState
        GetState = stateContainer.GetState
    }
        
    let processCommand = createCommandProcessor channel matcher
    
    let processSocketMsg msg = async {
        printfn "%s" msg // todo replace to logger
        let {MessageId = id; Request = request} = Serializer.deserializeClientMessage msg
        let! response = processCommand request
        pushResponse id response
    }
    
    let errorAction (e: exn) = async {
        logger.LogError(e, "Error in read loop")
        pushResponse String.Empty (ErrorResponse InternalErrorResponse)
    }

    let closeConnection() = async {
        do! processCommand DisconnectCommand |> Async.Ignore
        ctsNotificator.Cancel()
    }
    
    do! Socket.startReader connection processSocketMsg errorAction closeConnection CancellationToken.None

//    let rec readLoop() = async {
//        let! result = readSocket()
//        match result.CloseStatus.HasValue with
//        | false -> 
//            try
//                let msg = Encoding.UTF8.GetString(buffer, 0, result.Count)
//                printfn "%s" msg // todo replace to logger
//                let id, command = JsonSerializer.deserializeRequest msg
//                let! response = processCommand command
//                pushResponse id response
//            with e ->
//                logger.LogError(e, "Error in read loop")
//                pushResponse -1 (ErrorResponse InternalErrorResponse)
//            return! readLoop()
//        | true -> 
//            do! closeConnection result.CloseStatus.Value result.CloseStatusDescription
//    }
//    do! readLoop()
}


