module ChessConnection

open System.Net.WebSockets
open Types.Command
open System
open System.Threading
open System.Threading.Tasks

type ServerConnection = {
    Ping: PingCommand -> CancellationToken -> Async<PingResponse>
    Start: unit -> Async<unit>
    Close: unit -> unit
}

let createConnection url processError processDisconnect = async {
    let cts = new CancellationTokenSource()
    let ct = cts.Token
    
    let socket = new ClientWebSocket()
    do! socket.ConnectAsync(url, ct) |> Async.AwaitTask

    let cts = new CancellationTokenSource()
    let ct = cts.Token

    let write msg = Socket.write socket msg ct

    let generateMessageId() = Guid.NewGuid().ToString()
    let checkMessageId msgId response = msgId = response.MessageId

    let inputResponses = Event<ResponseDto>()
    let inputNotifies = Event<Notify>()
    
    let readMsg msg =
        let serverMessage = Serializer.deserializeServerMessage msg
        match serverMessage with
        | Response r -> inputResponses.Trigger r
        | Notification n -> inputNotifies.Trigger n
        async.Return ()
    
    let reader = Socket.startReader socket readMsg processError processDisconnect ct
    
    let ping command ct = async {
        let messageId = generateMessageId()
        let sub = inputResponses.Publish |> Event.filter (checkMessageId messageId)
        let resultTask = Async.StartAsTask(Async.AwaitEvent sub, TaskCreationOptions.None, ct)
        let request = {MessageId = messageId; Request = PingCommand command}
        do! Serializer.serializeRequest request |> write
        let! msg = resultTask |> Async.AwaitTask //or task
        match msg.Response with
        | PingResponse r -> return r
        | _ -> return failwith "Invalid response command"
    }
    
    return {
        Ping = ping
        Start = fun () -> reader
        Close = fun () -> cts.Cancel()
    }
}