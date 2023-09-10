module ChessServer.Network.Server

open Suave
open Suave.Operators
open Suave.Filters
open Suave.Sockets
open Suave.Sockets.Control
open Suave.WebSocket
open Suave.Files
open Suave.RequestErrors

open System
open Microsoft.AspNetCore.SignalR
open System.Threading.Tasks
open ChessServer.Common
open ChessServer.Types.Channel
open Types.Command
open Microsoft.Extensions.Logging
open ChessServer.CommandProcessor
open System.Collections.Concurrent
open FSharp.Control.Tasks.V2
open System.Threading
open ChessServer.ClientChannelManager
open Microsoft.Extensions.Configuration
open ChessServer
open ChannelManager

[<AutoOpen>]
module private Internal =
    let logger = Logging.getLogger "CommandProcessorHub"

    [<AutoOpen>]
    module private Internal = 
        let disconnectQuery = ConcurrentDictionary<string, Timer>()
    
        let matcher = MatchManager.createMatcher()
    
        


    let processCommand = processCommand matcher

    


    
//type CommandProcessorHub(context: HubContextAccessor, config: IConfiguration) = 
  //  inherit Hub()


    //member private this.GetChannel() = channels.TryGetValue this.Context.ConnectionId |> snd

    //override this.OnConnectedAsync() = 
    //    let connectionId = this.Context.ConnectionId
    //    let channel = createChannel connectionId this.SendNotify
    //    if not <| channels.TryAdd(connectionId, channel) then
    //        logger.LogError("Channel {0} error: can't add channel", connectionId)
    //        Task.CompletedTask
    //    else 
    //        logger.LogInformation("Channel {0} connected. Active connections: {x}", connectionId, channels.Count)
    //        base.OnConnectedAsync()

    //override this.OnDisconnectedAsync(exn) =
    //    let connectionId = this.Context.ConnectionId

    //    let disconnect() =
    //        this.Disconnect() |> ignore
    //        channels.TryRemove connectionId |> ignore
    //        logger.LogInformation("Channel {0} disconnected. Active connections: {x}", connectionId, channels.Count)    

    //    let channel = this.GetChannel()
    //    let state = channel.GetState()
    //    match state with
    //    | Matched _ ->
    //        channel.Disconnect()
    //        addDisconnectTimer (fun () ->
    //            processCommand channel DisconnectCommand |> ignore
    //            channels.TryRemove connectionId |> ignore
    //            logger.LogInformation("Removed channel {ch} due timeout", channel.Id)
    //        ) disconnectTimeout connectionId
    //    | _ ->
    //        disconnect()
    //    base.OnDisconnectedAsync(exn)
        
    //member private this.SendNotify(id, notif) =
    //    let client = context.GetContext<CommandProcessorHub>().Clients.Client(id)
    //    client.SendAsync("notification", Serializer.serializeNotify notif)
    //    |> ignore

    //member private this.ProcessCommand request = 
    //     let channel = this.GetChannel()
    //     //logger.LogInformation(sprintf "Processing command %A" request)
    //     processCommand channel request |> Serializer.serializeResponse
    
    //member this.Ping msg =
    //    this.ProcessCommand <| PingCommand {Message = msg}
         
    //member this.Match optionsRaw =
    //    let options = Serializer.deserializeMatchOptions optionsRaw
    //    this.ProcessCommand <| MatchCommand options

    //member this.Chat message =
    //    this.ProcessCommand <| ChatCommand {Message = message}

    //member this.Move moveCommand = 
    //    let move = Serializer.deserializeMoveCommand moveCommand
    //    this.ProcessCommand <| MoveCommand move

    //member this.Restore oldConnectionId =
    //    let getError = ReconnectError >> ErrorResponse
    //    let newConnectionId = this.Context.ConnectionId
    //    let channel = this.GetChannel()
    //    match channel.GetState() with
    //    | New ->
    //        try
    //            // todo monad
    //            let defaultMsg = "Invalid channel"
    //            if newConnectionId = oldConnectionId then failwith defaultMsg
    //            let exists, oldChannel = channels.TryGetValue oldConnectionId
    //            exists |> checkTrue defaultMsg
    //            tryRemoveDisconnectTimer oldConnectionId |> checkTrue defaultMsg
    //            channels.TryRemove oldConnectionId |> fst |> checkTrue "Internal error 1"
    //            oldChannel.Reconnect newConnectionId
    //            channels.TryUpdate(newConnectionId, oldChannel, channel) |> checkTrue "Internal error 2"
    //            OkResponse
    //        with e ->
    //            getError e.Message
    //    | _ -> getError "Only new channel can do restore"
    //    |> Serializer.serializeResponse

    //member this.Disconnect() =
    //    this.ProcessCommand DisconnectCommand

    //member this.TestDisconnect() =
    // todo migrate
    //    this.Context.Abort()

let private ws (webSocket : WebSocket) (context: HttpContext) =
    //let getChannel() = channels.TryGetValue this.Context.ConnectionId |> snd

    let send (msg: string) = 
        socket {
            let byteResponse =
                msg
                |> System.Text.Encoding.ASCII.GetBytes
                |> ByteSegment
            do! webSocket.send Text byteResponse true
        }

    use writeAgent = MailboxProcessor<string>.Start(fun inbox ->
        let rec messageLoop() = async {
            let! msg = inbox.Receive()
            let! r = send msg

            match r with 
            | Choice1Of2 () -> 
                ()
            | Choice2Of2 err -> 
                match err with 
                | SocketError s -> 
                    printfn "Socket error: %s" (s.ToString())
                | InputDataError (statusCodeOption, msg) -> 
                    let statusCode = 
                        match statusCodeOption with 
                        | Some code -> sprintf "%d" code
                        | None -> "400 (default)"
                    printfn "Input data error: status code - %s, message - %s" statusCode msg
                | ConnectionError msg -> 
                    printfn "Connection error: %s" msg

            //match r with
            //// Error case
            //| Suave.Sockets.Error e ->
            //    // Example error handling logic here
            //    printfn "Error: [%A]" error
            //    exampleDisposableResource.Dispose()

            // todo
            //match r with
            //| Error -> ""
            return! messageLoop()
        }
        messageLoop()
    )

    let writeSocket (msg: string) = 
        logger.LogInformation("Write message {s}", msg)
        printfn "%s" msg
        writeAgent.Post msg

    //let ctsNotificator = new CancellationTokenSource()

    let pushMessage serialize obj =
        let json = serialize obj
        writeSocket json

    let pushNotify = pushMessage Serializer.serializeNotify
    let pushResponse id = pushMessage (Serializer.serializeResponse id)

    let connectionId = ConnectionId(Guid.NewGuid().ToString())
    let channel = createChannel connectionId pushNotify
    //channels.TryAdd(connectionId, channel) |> ignore
    channelManager.Add channel
    logger.LogInformation("Channel {0} connected. Active connections: {x}", connectionId, channelManager.Count())

    let processCommand = processCommand channelManager channel

    socket {
        let mutable loop = true
        let emptyResponse = [||] |> ByteSegment

        while loop do
            let! msg = webSocket.read()
            
            match msg with
            | (Text, data, true) ->
                let str = UTF8.toString data
                let (id, request) = Serializer.deserializeClientMessage str
                let response = processCommand request
                pushResponse id response

            | (Close, _, _) ->
                let disconnect() = socket {
                    loop <- false
                    logger.LogInformation("Channel {0} disconnected. Active connections: {x}", connectionId, channelManager.Count())    
                }

                //let channel = this.GetChannel()
                let state = channel.GetState()
                match state with
                | Matched _ ->
                    channel.Disconnect()
                    channelManager.AddDisconnectTimeout (fun () ->
                        processCommand DisconnectCommand |> ignore
                    ) connectionId
                | _ ->
                    do! disconnect()
                    channelManager.Remove connectionId
                do! webSocket.send Close emptyResponse true

            | _ -> ()
    }

let app : WebPart =
    choose [
        path "/ws" >=> handShake ws
        GET >=> choose [ path "/" >=> file "index.html"; browseHome ]
        NOT_FOUND "Found no handlers." ]