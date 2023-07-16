module ChessServer.Hubs

//open System
//open Microsoft.AspNetCore.SignalR
//open System.Threading.Tasks
//open ChessServer.Common
//open Types.Channel
//open Types.Command
//open Microsoft.Extensions.Logging
//open CommandProcessor
//open HubContextAccessor
//open System.Collections.Concurrent
//open FSharp.Control.Tasks.V2
//open System.Threading
//open ClientChannelManager
//open Microsoft.Extensions.Configuration

//[<AutoOpen>]
//module private Internal =
//    let logger = Logging.getLogger "CommandProcessorHub"
    
//    let channels = ConcurrentDictionary<string, ClientChannel>()
//    let disconnectQuery = ConcurrentDictionary<string, Timer>()
    
//    let matcher = MatchManager.createMatcher()

//    let processCommand = processCommand matcher
    
//    let tryRemoveDisconnectTimer channel = 
//        let exists, timer = disconnectQuery.TryRemove channel
//        if exists then
//            timer.Dispose()
//            true
//        else
//            false

//    let addDisconnectTimer f (timeout: TimeSpan) channel = 
//        let timer = new Timer(fun state ->
//            try
//                f()
//                tryRemoveDisconnectTimer channel |> ignore
//            with e ->
//                logger.LogError(e, "Error in disconnect timer")
//        )
//        if disconnectQuery.TryAdd(channel, timer) then
//            timer.Change(timeout, Timeout.InfiniteTimeSpan) |> ignore
//        else
//            failwithf "Can't add disconnect timer for channel %s - already exists" channel
            
//    let checkTrue msg x = if not x then failwith msg

//type CommandProcessorHub(context: HubContextAccessor, config: IConfiguration) = 
//    inherit Hub()

//    let disconnectTimeout = TimeSpan.FromSeconds(config.GetValue<double>("DisconnectTimeout"))

//    member private this.GetChannel() = channels.TryGetValue this.Context.ConnectionId |> snd

//    override this.OnConnectedAsync() = 
//        let connectionId = this.Context.ConnectionId
//        let channel = createChannel connectionId this.SendNotify
//        if not <| channels.TryAdd(connectionId, channel) then
//            logger.LogError("Channel {0} error: can't add channel", connectionId)
//            Task.CompletedTask
//        else 
//            logger.LogInformation("Channel {0} connected. Active connections: {x}", connectionId, channels.Count)
//            base.OnConnectedAsync()

//    override this.OnDisconnectedAsync(exn) =
//        let connectionId = this.Context.ConnectionId

//        let disconnect() =
//            this.Disconnect() |> ignore
//            channels.TryRemove connectionId |> ignore
//            logger.LogInformation("Channel {0} disconnected. Active connections: {x}", connectionId, channels.Count)    

//        let channel = this.GetChannel()
//        let state = channel.GetState()
//        match state with
//        | Matched _ ->
//            channel.Disconnect()
//            addDisconnectTimer (fun () ->
//                processCommand channel DisconnectCommand |> ignore
//                channels.TryRemove connectionId |> ignore
//                logger.LogInformation("Removed channel {ch} due timeout", channel.Id)
//            ) disconnectTimeout connectionId
//        | _ ->
//            disconnect()
//        base.OnDisconnectedAsync(exn)
        
//    member private this.SendNotify(id, notif) =
//        let client = context.GetContext<CommandProcessorHub>().Clients.Client(id)
//        client.SendAsync("notification", Serializer.serializeNotify notif)
//        |> ignore

//    member private this.ProcessCommand request = 
//         let channel = this.GetChannel()
//         //logger.LogInformation(sprintf "Processing command %A" request)
//         processCommand channel request |> Serializer.serializeResponse
    
//    member this.Ping msg =
//        this.ProcessCommand <| PingCommand {Message = msg}
         
//    member this.Match optionsRaw =
//        let options = Serializer.deserializeMatchOptions optionsRaw
//        this.ProcessCommand <| MatchCommand options

//    member this.Chat message =
//        this.ProcessCommand <| ChatCommand {Message = message}

//    member this.Move moveCommand = 
//        let move = Serializer.deserializeMoveCommand moveCommand
//        this.ProcessCommand <| MoveCommand move

//    member this.Restore oldConnectionId =
//        let getError = ReconnectError >> ErrorResponse
//        let newConnectionId = this.Context.ConnectionId
//        let channel = this.GetChannel()
//        match channel.GetState() with
//        | New ->
//            try
//                // todo monad
//                let defaultMsg = "Invalid channel"
//                if newConnectionId = oldConnectionId then failwith defaultMsg
//                let exists, oldChannel = channels.TryGetValue oldConnectionId
//                exists |> checkTrue defaultMsg
//                tryRemoveDisconnectTimer oldConnectionId |> checkTrue defaultMsg
//                channels.TryRemove oldConnectionId |> fst |> checkTrue "Internal error 1"
//                oldChannel.Reconnect newConnectionId
//                channels.TryUpdate(newConnectionId, oldChannel, channel) |> checkTrue "Internal error 2"
//                OkResponse
//            with e ->
//                getError e.Message
//        | _ -> getError "Only new channel can do restore"
//        |> Serializer.serializeResponse

//    member this.Disconnect() =
//        this.ProcessCommand DisconnectCommand

//    member this.TestDisconnect() =
//        this.Context.Abort()