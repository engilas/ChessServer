module ChessConnection

open System.Net.WebSockets
open Types.Command
open System
open System.Threading

type ServerConnection = {
    Ping: PingCommand -> Async<PingResponse>
    Start: unit -> Async<unit>
    Close: unit -> unit
}

let createConnection url = async {
    let socket = new ClientWebSocket()
    do! socket.ConnectAsync url |> Async.AwaitTask

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
    
    let reader = Socket.startReader socket readMsg (fun _ -> async.Return ()) (fun _ -> async.Return ()) ct
    
    let ping command = async {
        let messageId = generateMessageId()
        let sub = inputResponses.Publish |> Event.filter (checkMessageId messageId)
        let resultTask = Async.AwaitEvent sub |> Async.StartAsTask
        let request = {MessageId = messageId; Request = PingCommand command}
        do! Serializer.serializeRequest request |> write
        let! msg = resultTask |> Async.AwaitTask //or task
        match msg.Response with
        | PingResponse r -> return r
        | _ -> return failwith ""
    }
    
    return {
        Ping = ping
        Start = fun () -> reader
        Close = fun () -> cts.Cancel()
    }
}