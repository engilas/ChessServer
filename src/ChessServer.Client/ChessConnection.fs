module ChessServer.Client.ChessConnection

open Microsoft.AspNetCore.SignalR.Client
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open ChessServer.Common
open Types.Command
open Types.Domain
open System
open FSharp.Control.Tasks.V2
open System.Net.WebSockets
open System.Threading
open System.Text

type NotificationHandler = {
    ChatNotification: string -> unit
    MoveNotification: MoveDescription -> unit
    EndGameNotification: EndGameNotify -> unit
    SessionStartNotification: SessionStartNotify -> unit
    SessionCloseNotification: SessionCloseReason -> unit
}

let notificationHandlerAdapter
    (chatNotification: Action<string>)
    (moveNotification: Action<MoveDescription>)
    (endGameNotification: Action<EndGameNotify>)
    (sessionStartNotification: Action<SessionStartNotify>)
    (sessionCloseNotification: Action<SessionCloseReason>)
    =
    {
        ChatNotification = chatNotification.Invoke
        MoveNotification = moveNotification.Invoke
        EndGameNotification = endGameNotification.Invoke
        SessionStartNotification = sessionStartNotification.Invoke
        SessionCloseNotification = sessionCloseNotification.Invoke
    }

let private checkCommonResponse x =
    match x with
    | OkResponse
    | ErrorResponse _ -> x
    | _ -> failwithf "Invalid response %A" x
    
let private parseResponse (x: Task<_>) = task {
    let! rawResponse = x
    return Serializer.deserializeResponse rawResponse
}

//let runWebSocketClient uri =
    

    

    

    

//    // Wait until user presses Enter, then close the websocket
//    Console.ReadLine() |> ignore
//    cts.Cancel()
//    client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None) |> Async.AwaitTask |> Async.RunSynchronously

//type ServerConnection2 (url: string, notificationHandler) =

    
        
    //member this.Connect() = connect()

type ServerConnection (url: string, notificationHandler) =
    // todo auto reconnect
    let delayIntervals = 
        Array.replicate 10 3
        |> Array.map (double >> TimeSpan.FromSeconds)

    let mutable connectionId = ConnectionId ""

    let cts = new CancellationTokenSource()
    let client = new ClientWebSocket()

    let generateMessageId() = MessageId(Guid.NewGuid().ToString())
    let checkMessageId msgId response = msgId = (response |> fst)

    let inputResponses = Event<ResponseDto>()

    let notificationHandler n =
        match n with
        | ChatNotify n -> notificationHandler.ChatNotification n.Message
        | MoveNotify n -> notificationHandler.MoveNotification n
        | EndGameNotify n -> notificationHandler.EndGameNotification n
        | SessionStartNotify n -> notificationHandler.SessionStartNotification n
        | SessionCloseNotify n -> notificationHandler.SessionCloseNotification n
    
    let rec receiveMessage (ws: ClientWebSocket) (ct: CancellationToken) =
        async {
            let buffer = Array.zeroCreate 1024
            let segment = new ArraySegment<byte>(buffer)
            let! result = ws.ReceiveAsync(segment, ct) |> Async.AwaitTask
            let msg = Encoding.UTF8.GetString(buffer, 0, result.Count)
            //printfn "Received: %s" message

            let serverMessage = Serializer.deserializeServerMessage msg
            match serverMessage with
            | Response r -> inputResponses.Trigger r
            | Notification n -> notificationHandler n

            if ws.State = WebSocketState.Open then
                return! receiveMessage ws ct
        }
    
    let sendMessage (message: string) =
        let bytes = Encoding.UTF8.GetBytes(message)
        let segment = new ArraySegment<byte>(bytes)
        client.SendAsync(segment, WebSocketMessageType.Text, true, cts.Token)

    let getResponse ct request = task {
        let combinedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token).Token
        let messageId = generateMessageId()
        let sub = inputResponses.Publish |> Event.filter (checkMessageId messageId)
        let resultTask = Async.StartAsTask(Async.AwaitEvent sub, TaskCreationOptions.None, combinedCt)
        do! Serializer.serializeClientMessage (messageId, request) |> sendMessage
        let! result = resultTask
        return result |> snd
    }

    let getResponse request = getResponse (CancellationToken.None) request

    let connect (uri: Uri) =
        client.ConnectAsync(uri, cts.Token)// |> Async.AwaitTask |> Async.RunSynchronously


    let start (uri: string) = task {
        do! connect (new Uri(uri))
        receiveMessage client cts.Token |> Async.Start
        let! response = getResponse GetConnectionId
        connectionId <- 
            match response with
            | ConnectionIdResponse id -> id
            | _ -> failwith "GetConnectionId: Invalid response"
    }

    

    //interface IAsyncDisposable with
    //    member this.DisposeAsync() = 
    //        let q = task {
    //            cts.Cancel()
    //            do! client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None)
    //            client.Dispose() 
    //        }
    //        ValueTask(q)

    //let socket = new ClientWebSocket()
    //let write msg = Socket.write socket msg

    //let connection =
    //    (new HubConnectionBuilder())
    //        .WithUrl(url)
    //        .WithAutomaticReconnect(delayIntervals)
    //        .ConfigureLogging(fun c -> c.SetMinimumLevel(LogLevel.Information) |> ignore)
    //        .Build()
            
    
        
    //let reconnectedEvent =
    //  { new IDelegateEvent<_> with
    //        member this.AddHandler x = connection.add_Reconnected x
    //        member this.RemoveHandler x = connection.remove_Reconnected x }
      
    //let reconnectingEvent =
    //  { new IDelegateEvent<_> with
    //        member this.AddHandler x = connection.add_Reconnecting x
    //        member this.RemoveHandler x = connection.remove_Reconnecting x }


    //let closedEvent =
    //  { new IDelegateEvent<_> with
    //        member this.AddHandler x = connection.add_Closed x
    //        member this.RemoveHandler x = connection.remove_Closed x }

    //let invokeCommand method args checker = task {
    //    let! response =
    //        connection.InvokeCoreAsync<_>(method, Array.ofList args)
    //        |> parseResponse
    //    return checker response
    //}
        
    //let mutable closed = false
    
    //member this.DisposeAsync() =
    //     task {
    //         notifListener.Dispose()
    //         if not closed then
    //             do! connection.StopAsync().ConfigureAwait(false)
    //         do! connection.DisposeAsync().ConfigureAwait(false)
    //     } |> ValueTask
         
    //member this.Dispose() = this.DisposeAsync().ConfigureAwait(false).GetAwaiter().GetResult()
        
    interface IDisposable with
         member this.Dispose() = 
            client.Dispose()
            ()
             //notifListener.Dispose()
             //if not closed then
                 //cconn
             //do! connection.DisposeAsync().ConfigureAwait(false)
    


    member this.Connect() = start(url)

    //member this.Connect(ct) = connection.StartAsync(ct)
    
    //[<CLIEvent>]
    //member this.Reconnected = reconnectedEvent
    
    //[<CLIEvent>]
    //member this.Reconnecting = reconnectingEvent

    //[<CLIEvent>]
    //member this.Closed = closedEvent
    
    // todo 
    member this.GetConnectionId() = connectionId
    
    //member this.GetHubConnection() = connection

    
    
    member this.Ping() = PingCommand {Message = "ping"} |> getResponse
    //task {
    //    let msg = Guid.NewGuid().ToString()
    //    let! response = PingCommand {Message = msg} |> getResponse
    //    match response with
    //    | PingResponse {Message = x} when x = msg -> response
    //    | _ -> failwith "Invalid response command"
    //}
        //invokeCommand "ping" [msg] (fun response ->
        //    match response with
        //    | PingResponse {Message = x} when x = msg -> response
        //    | ErrorResponse _ -> response
        //    | _ -> failwithf "Invalid response %A" response
        //)
    

    member this.Match options = MatchCommand options |> getResponse
    member this.Chat msg = ChatCommand {Message = msg} |> getResponse
    member this.Move command = MoveCommand command |> getResponse
    member this.Disconnect() = DisconnectCommand |> getResponse
    member this.Restore id = ReconnectCommand {OldConnectionId = id} |> getResponse
    
    member this.Close() = task {
        //do! this.Disconnect() :> Task
        do! client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", cts.Token)
        cts.Cancel()
        //closed <- true
    }

    //member this.TestDisconnect() =
    //    connection.InvokeAsync("TestDisconnect")