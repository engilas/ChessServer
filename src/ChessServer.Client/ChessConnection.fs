module ChessConnection

open Microsoft.AspNetCore.SignalR.Client
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Types.Command
open Types.Domain
open System
open FSharp.Control.Tasks.V2

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

type ServerConnection (url: string, notificationHandler) =
    let connection =
        (new HubConnectionBuilder())
            .WithUrl(url)
            .WithAutomaticReconnect()
            .ConfigureLogging(fun c -> c.SetMinimumLevel(LogLevel.Information) |> ignore)
            .Build()
            
    let notifListener =
        connection.On("notification", fun notifRaw ->
            let notif = Serializer.deserializeNotify notifRaw
            match notif with
            | ChatNotify n -> notificationHandler.ChatNotification n.Message
            | MoveNotify n -> notificationHandler.MoveNotification n
            | EndGameNotify n -> notificationHandler.EndGameNotification n
            | SessionStartNotify n -> notificationHandler.SessionStartNotification n
            | SessionCloseNotify n -> notificationHandler.SessionCloseNotification n
        )
        
    let mutable closed = false
        
    interface IDisposable with
         member this.Dispose() =
             notifListener.Dispose()
             if not closed then
                 this.Close().GetAwaiter().GetResult()
             connection.DisposeAsync().GetAwaiter().GetResult()
             
    member this.Connect() = connection.StartAsync()
    
    member this.Ping() = task {
        let msg = Guid.NewGuid().ToString()
        let! response = connection.InvokeAsync<_>("ping", msg) |> parseResponse
        return
            match response with
            | PingResponse {Message = x} when x = msg -> response
            | ErrorResponse _ -> response
            | _ -> failwithf "Invalid response %A" response 
    }
    
    member this.Match options = task {
        let! response =
            connection.InvokeAsync<_>("match", Serializer.serializeMatchOptions options)
            |> parseResponse
        return checkCommonResponse response
    }
        
    member this.Chat (msg: string) = task {
        let! response =
            connection.InvokeAsync<_>("chat", msg)
            |> parseResponse
        return checkCommonResponse response
    }
        
     member this.Move command = task {
         let! response =
             connection.InvokeAsync<_>("move", Serializer.serializeMoveCommand command)
             |> parseResponse
         return checkCommonResponse response
    }
         
    member this.Disconnect() = task {
        let! response =
            connection.InvokeAsync<_>("disconnect")
            |> parseResponse
        return checkCommonResponse response
    }
    
     member this.Close() = task {
         do! this.Disconnect() :> Task
         do! connection.StopAsync()
         closed <- true
     }