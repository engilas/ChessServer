module ChessConnection

open System.Net.WebSockets
open Types.Command

type ServerConnection = {
    Ping: PingCommand -> Async<PingResponse>
}

let createConnection url = async {
    let socket = new ClientWebSocket()
    do! socket.ConnectAsync url |> Async.AwaitTask
    let input = Event<string>()
    
    let readMsg msg = async {
        input.Trigger msg
    }
    
    let reader = Socket.startReader socket readMsg (fun _ -> async.Return ()) (fun _ -> async.Return ())
    
    let ping command = async {
        let sub = input.Publish |> Event.filter ((=) "some msg")
        let resultTask = Async.AwaitEvent sub |> Async.StartAsTask
        //do send ping
        return! resultTask |> Async.AwaitTask //or task
    }
    0
}