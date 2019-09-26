module Socket

open System.Net.WebSockets
open System.Text
open System
open System.Threading
open FSharp.Control.Tasks.V2

let read (connection: WebSocket) buffer ct =
    let rec readInternal total = task {
        let! response = connection.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
        if response.CloseStatus.HasValue then
            return response, total
        else
            let total = total @ (new ArraySegment<byte>(buffer, 0, response.Count) |> List.ofSeq)
            if response.EndOfMessage
            then return response, total
            else return! readInternal total
    }
    readInternal []

let write (connection: WebSocket) msg ct =
    let bytes = Encoding.UTF8.GetBytes(msg:string)
    connection.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct)
        |> Async.AwaitTask
        
let startReader (connection: WebSocket) processMsg processError processDisconnect ct = async {
    //todo проверить хватит ли размера буффера
    //todo попробовать передать большие сообщение, посмотреть как было реализовано в агаторе
    let buffer: byte[] = Array.zeroCreate 4096

    let closeConnection closeStatus closeDesc = async {
        do! processDisconnect()
        //if connection.State = WebSocketState.
        let caasedq = connection
        do! connection.CloseAsync(closeStatus, closeDesc, CancellationToken.None)
            |> Async.AwaitTask
    }

    let rec readLoop() = async {
        let! response, bytes = read connection buffer ct |> Async.AwaitTask
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
    }
    
    try
        return! readLoop()
    with e -> 
        match e with
        //| :? OperationCanceledException -> 
        //    do! closeConnection WebSocketCloseStatus.NormalClosure "Connection closed"
        | e ->
            let qqe = 5
            do! Async.Sleep 20000
            do! processError e
            //do! closeConnection WebSocketCloseStatus.InternalServerError "Internal error occured"
    
}