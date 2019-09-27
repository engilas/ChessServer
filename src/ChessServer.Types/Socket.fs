module Socket

open System.Net.WebSockets
open System.Text
open System
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2

let canRead (socket: WebSocket) = socket.State = WebSocketState.Open || socket.State = WebSocketState.CloseSent
let canWrite (socket: WebSocket) = socket.State = WebSocketState.Open || socket.State = WebSocketState.CloseReceived

let read (socket: WebSocket) buffer =
    let rec readInternal total = task {
        if canRead socket then
            let! response = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
            if response.CloseStatus.HasValue then
                return Some (response, total)
            else
                let total = total @ (new ArraySegment<byte>(buffer, 0, response.Count) |> List.ofSeq)
                if response.EndOfMessage
                then return Some (response, total)
                else return! readInternal total
        else return None
    }
    readInternal []

let write (socket: WebSocket) msg ct =
    if canWrite socket then
        let bytes = Encoding.UTF8.GetBytes(msg:string)
        socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct)
    else Task.CompletedTask

        
let startReader (connection: WebSocket) processMsg processError processDisconnect ct = async {
    //todo проверить хватит ли размера буффера
    //todo попробовать передать большие сообщение, посмотреть как было реализовано в агаторе
    let buffer: byte[] = Array.zeroCreate 4096

    let closeConnection closeStatus closeDesc = async {
        do! processDisconnect()
        do! connection.CloseAsync(closeStatus, closeDesc, CancellationToken.None)
            |> Async.AwaitTask
    }

    let rec readLoop() = async {
        let! readResult = read connection buffer |> Async.AwaitTask
        match readResult with
        | Some (response, bytes) ->
            match response.CloseStatus.HasValue with
            | false ->
                try
                    let msg = Encoding.UTF8.GetString(bytes |> Array.ofList)
                    do! processMsg msg
                with e ->
                    do! processError e
                return! readLoop()
            | true ->
                do! closeConnection response.CloseStatus.Value response.CloseStatusDescription
        | None -> ()
    }
    
    try
        return! readLoop()
    with e -> 
        match e with
        | e ->
            do! processError e
            do! closeConnection WebSocketCloseStatus.InternalServerError "Internal error occured"
    
}