namespace ChessServer

open CommandTypes
open ChannelTypes

module SocketManager =
    open System
    open System.Net.WebSockets
    open System.Threading
    open System.Text
    open Microsoft.Extensions.Logging

    let logger = Logging.getLogger "SocketManager"

    let rec startNotificator sendNotify (ct: CancellationToken) = async {
        do sendNotify (TestNotify {Message = "test"})
        do! Async.Sleep 100000

        if not ct.IsCancellationRequested then 
            return! startNotificator sendNotify ct
    }

    open CommandProcessor
    
    let processConnection (connection: WebSocket) connectionId = async {
        do! Async.Sleep 1

        let buffer : byte[] = Array.zeroCreate 4096

        let readSocket() = connection.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
                            |> Async.AwaitTask

        use writeAgent = MailboxProcessor.Start(fun inbox ->
            let rec messageLoop() = async {
                let! msg = inbox.Receive()
                let bytes = Encoding.UTF8.GetBytes(msg:string)
                do! connection.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
                    |> Async.AwaitTask
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

        let pushNotify = pushMessage JsonRpc.serializeNotify
        let pushResponse id = pushMessage (JsonRpc.serializeResponse id)

        startNotificator pushNotify ctsNotificator.Token 
        |> Async.Start
        
        let processCommand, changeState = createCommandProcessor connectionId

        let clientChannel : ClientChannel = {
            Id = connectionId
            PushNotification = pushNotify
            ChangeState = changeState
        }

        let closeConnection closeStatus description = async {
            do! processCommand DisconnectCommand clientChannel |> Async.Ignore

            ctsNotificator.Cancel()
            do! connection.CloseAsync(closeStatus, description, CancellationToken.None)
                |> Async.AwaitTask
        }

        let rec readLoop() = async {
            let! result = readSocket()
            match result.CloseStatus.HasValue with
            | false -> 
                try
                    let msg = Encoding.UTF8.GetString(buffer, 0, result.Count)
                    printfn "%s" msg
                    let id, command = JsonRpc.deserializeRequest msg
                    let! response = processCommand command clientChannel
                    match response with
                    | Some r -> pushResponse id r
                    | None -> ()
                with e ->
                    logger.LogError(e, "Error in read loop")
                    let error = ErrorResponse {Message = sprintf "Error: %s" e.Message}
                    pushResponse -1 error
                return! readLoop()
            | true -> 
                do! closeConnection result.CloseStatus.Value result.CloseStatusDescription
        }
        do! readLoop()
    }


