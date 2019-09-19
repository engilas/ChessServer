module Socket

open System.Net.WebSockets
open System.Text
open System
open System.Threading


let read (connection: WebSocket) buffer ct =
    connection.ReceiveAsync(new ArraySegment<byte>(buffer), ct)
    |> Async.AwaitTask

let write (connection: WebSocket) msg ct = async { 
    let bytes = Encoding.UTF8.GetBytes(msg:string)
    do! connection.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct)
        |> Async.AwaitTask
}

let startReader (connection: WebSocket) processMsg processError processDisconnect ct = async {
    //todo проверить хватит ли размера буффера
    //todo попробовать передать большие сообщение, посмотреть как было реализовано в агаторе
    let buffer: byte[] = Array.zeroCreate 4096
    let rec readLoop() = async {
        let! result = read connection buffer ct
        match result.CloseStatus.HasValue with
        | false ->
            try
                let msg = Encoding.UTF8.GetString(buffer, 0, result.Count)
                do! processMsg msg
            with e ->
                do! processError e
            return! readLoop()
        | true ->
            do! processDisconnect()
            do! connection.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None)
                |> Async.AwaitTask
    }
    return! readLoop()
}