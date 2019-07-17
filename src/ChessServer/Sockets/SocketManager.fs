namespace ChessServer

open Types

module JsonRpc =
    open Anemonis.JsonRpc

    module private Internal =
        open Newtonsoft.Json.Linq

        let contracts = new JsonRpcContractResolver()
        let serializer = new JsonRpcSerializer(contracts)

        let serializeNotify method obj =
            let id = Unchecked.defaultof<JsonRpcId>
            let objParams = [obj]
            let request = new JsonRpcRequest(&id, method, objParams)
            serializer.SerializeRequest(request)

        let serializeResponse id obj =
            let id = JsonRpcId.FromString(id)
            let response = new JsonRpcResponse(&id, result = obj)
            serializer.SerializeResponse(response)

        let deserializeRequest (str:string) =
            let data = serializer.DeserializeRequestData(str)

            match data.Item.Message.Method with
            | "ping" -> 
                PingCommand (JObject.FromObject(data.Item.Message.ParametersByName).ToObject<PingCommand>())
            | "pung" ->
                PungCommand (JObject.FromObject(data.Item.Message.ParametersByName).ToObject<PungCommand>())
            | x -> failwithf "unknown method %s" x

    open Internal

    let serializeNotify command =
        match command with
        | NotifyCommand c -> serializeNotify "notify" c
        | x -> failwithf "%A is not a notify" x

    let serializeResponse id command =
        match command with
        | PongCommand c -> serializeResponse id c

    let deserializeRequest = deserializeRequest


module SocketManager =
    open System.Threading.Tasks
    open System
    open System.Net.WebSockets
    open System.Threading
    open System.Text
    open System


    type private SocketState = {
        mutable Input: InputCommand list 
        mutable Output: OutputCommand list
    }

    //type SocketState = {
    //    Socket: WebSocket
    //    //State: TaskCompletionSource<Object>
    //}

    //let mutable private sockets = list<WebSocket>.Empty
    //let private addSocket x = sockets <- x :: sockets

    let rec startNotificator sendNotify (ct: CancellationToken) = async {
        do sendNotify (NotifyCommand {Message = "test"})
        do! Async.Sleep 1000

        if not ct.IsCancellationRequested then 
            return! startNotificator sendNotify ct
    }
    
    let processConnection (connection: WebSocket) = async {
        do! Async.Sleep 1

        let buffer : byte[] = Array.zeroCreate 4096

        let readSocket() = connection.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
                            |> Async.AwaitTask

        let writeAgent = MailboxProcessor.Start(fun inbox ->
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
                let command = JsonRpc.deserializeRequest msg
                CommandProcessor.processCommand command (pushResponse command.) pushNotify
                //do writeSocket msg
                //pushCommand 
                return! readLoop()
            | true -> 
                ctsNotificator.Cancel()
                do! connection.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None)
                    |> Async.AwaitTask
        }
        do! readLoop()
    }


