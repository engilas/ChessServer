module ServerSocketX

open System.Net.WebSockets
open System.Text
open System
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2

let canRead (socket: WebSocket) = socket.State = WebSocketState.Open || socket.State = WebSocketState.CloseSent
let canWrite (socket: WebSocket) = socket.State = WebSocketState.Open || socket.State = WebSocketState.CloseReceived  
let canClose (socket: WebSocket) = canWrite socket

let read (socket: WebSocket) buffer =
    let rec readInternal total = async {
        if canRead socket then
            let! response =
                socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None)
                |> Async.AwaitTask
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

let write (socket: WebSocket) msg =
    if canWrite socket then
        let bytes = Encoding.UTF8.GetBytes(msg:string)
        socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None)
    else Task.CompletedTask
   
let close (socket: WebSocket) closeStatus closeDesc = task {
    if canClose socket then
        try
            do! socket.CloseAsync(closeStatus, closeDesc, CancellationToken.None)
        with _ -> ()
}
        
let startReader (socket: WebSocket) processMsg processError processDisconnect = async {
    //todo проверить хватит ли размера буффера
    //todo попробовать передать большие сообщение, посмотреть как было реализовано в агаторе
    let buffer: byte[] = Array.zeroCreate 4096

    let closeConnection closeStatus closeDesc = async {
        processDisconnect()
        do! close socket closeStatus closeDesc |> Async.AwaitTask
    }

    let rec readLoop() = async {
        let! readResult = read socket buffer
        match readResult with
        | Some (response, bytes) ->
            if not response.CloseStatus.HasValue then
                try
                    let msg = Encoding.UTF8.GetString(bytes |> Array.ofList)
                    processMsg msg
                with e ->
                    processError e
                return! readLoop()
            else
                do! closeConnection response.CloseStatus.Value response.CloseStatusDescription
        | None -> ()
    }
    
    try
        return! readLoop()
    with e -> 
        match e with
        | e ->
            processError e
            do! closeConnection WebSocketCloseStatus.InternalServerError "Internal error occured"
}