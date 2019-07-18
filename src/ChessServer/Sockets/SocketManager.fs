namespace ChessServer

open Types

module SocketManager =
    open System
    open System.Net.WebSockets
    open System.Threading
    open System.Text

    let rec startNotificator sendNotify (ct: CancellationToken) = async {
        do sendNotify (TestNotify {Message = "test"})
        do! Async.Sleep 10000

        if not ct.IsCancellationRequested then 
            return! startNotificator sendNotify ct
    }
    
    let processConnection (connection: WebSocket) = async {
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

        let writeSocket (msg: string) = writeAgent.Post msg

        let ctsNotificator = new CancellationTokenSource()

        let pushMessage serialize obj =
            let json = serialize obj
            writeSocket json

        let pushNotify = pushMessage JsonRpc.serializeNotify
        let pushResponse id = pushMessage (JsonRpc.serializeResponse id)

        startNotificator pushNotify ctsNotificator.Token 
        |> Async.Start

        let rec readLoop () = async {
            let! result = readSocket()
            match result.CloseStatus.HasValue with
            | false -> 
                let msg = Encoding.UTF8.GetString(buffer, 0, result.Count)
                printfn "%s" msg
                let id, command = JsonRpc.deserializeRequest msg

                let clientChannel msg =
                    match msg with
                    | Response r -> pushResponse id r
                    | Notify n -> pushNotify n

                CommandProcessor.processCommand command clientChannel |> Async.Start
                return! readLoop()
            | true -> 
                ctsNotificator.Cancel()
                do! connection.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None)
                    |> Async.AwaitTask
        }
        do! readLoop()
    }


