module ChessConnection

open System.Net.WebSockets
open Types.Command
open System
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks.V2
open Types.Domain

type NotificationHandler = {
    ChatNotification: string -> unit
    MoveNotification: MoveDescription -> unit
    EndGameNotification: EndGameNotify -> unit
    SessionStartNotification: SessionStartNotify -> unit
    SessionCloseNotify: string -> unit
}

type ServerConnection (url, errorHandler, disconnectHandler, notificationHandler) =
    let cts = new CancellationTokenSource()
    let socket = new ClientWebSocket()
    let write msg = Socket.write socket msg cts.Token
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
        
    let getResponse ct request = task {
        let combinedCt = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token).Token
        let messageId = generateMessageId()
        let sub = inputResponses.Publish |> Event.filter (checkMessageId messageId)
        let resultTask = Async.StartAsTask(Async.AwaitEvent sub, TaskCreationOptions.None, combinedCt)
        let request = {MessageId = messageId; Request = request}
        do! Serializer.serializeRequest request |> write
        let! result = resultTask
        return result.Response
    }

    let notificationObserver =
        inputNotifies.Publish.Subscribe(fun notification ->
            match notification with
            | ChatNotify n -> notificationHandler.ChatNotification n.Message
            | MoveNotify n -> notificationHandler.MoveNotification n
            | EndGameNotify n -> notificationHandler.EndGameNotification n
            | SessionStartNotify n -> notificationHandler.SessionStartNotification n
            | SessionCloseNotify n -> notificationHandler.SessionCloseNotify n.Message
        )
    
    interface IDisposable with
        member this.Dispose() = this.Close()
        
    member this.Connect() = socket.ConnectAsync(url, cts.Token)
        
    member this.Start() = task {
        do! Socket.startReader socket readMsg errorHandler disconnectHandler cts.Token
    }
        
    member this.Ping msg ct = task {
        let! response = PingCommand {Message = msg} |> getResponse ct
        match response with
        | PingResponse {Message = x} when x = msg -> ()
        | _ -> failwith "Invalid response command"
    }
    
    member this.Match ct = task {
        let! response = MatchCommand |> getResponse ct
        match response with
        | OkResponse -> ()
        | _ -> failwith "Invalid response command"
    }
    
    member this.Chat msg ct = task {
        let! response = ChatCommand {Message = msg} |> getResponse ct
        match response with
        | OkResponse -> ()
        | _ -> failwith "Invalid response command"
    }
    
    member this.Move command ct = task {
        let! response = MoveCommand command |> getResponse ct
        match response with
        | OkResponse -> ()
        | _ -> failwith "Invalid response command"
    }
    
    member this.Disconnect ct = task {
        let! response = DisconnectCommand |> getResponse ct
        match response with
        | OkResponse -> ()
        | _ -> failwith "Invalid response command"
    }
        
    member this.Close() =
        cts.Cancel()
        notificationObserver.Dispose()