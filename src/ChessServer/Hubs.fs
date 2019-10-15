﻿module Hubs

open System
open Microsoft.AspNetCore.SignalR
open System.Threading.Tasks
open Types.Channel
open Types.Command
open Microsoft.Extensions.Logging
open StateContainer
open CommandProcessor
open HubContextAccessor
open System.Collections.Concurrent
open FSharp.Control.Tasks.V2
open System.Threading
open ClientChannelManager

[<AutoOpen>]
module private Internal =
    let logger = Logging.getLogger "CommandProcessorHub"
    
    let channels = ConcurrentDictionary<string, ClientChannel>()
    let disconnectQuery = ConcurrentDictionary<string, Timer>()
    
    let matcher = MatchManager.createMatcher()

    let serializeResponse (x: Task<_>) = task {
        let! response = x
        return Serializer.serializeResponse response
    }

    let processCommand = processCommand matcher
    
    let tryRemoveDisconnectTimer channel = 
        let exists, timer = disconnectQuery.TryRemove channel
        if exists then
            timer.Dispose()
            true
        else
            false

    let addDisconnectTimer f (timeout: TimeSpan) channel = 
        let timer = new Timer(fun state ->
            try
                f()
                tryRemoveDisconnectTimer channel |> ignore
            with e ->
                logger.LogError(e, "Error in disconnect timer")
        )
        if disconnectQuery.TryAdd(channel, timer) then
            timer.Change(timeout, Timeout.InfiniteTimeSpan) |> ignore
        else
            failwithf "Can't add disconnect timer for channel %s - already exists" channel

    
            
    let checkTrue msg x = if not x then failwith msg

type CommandProcessorHub(context: HubContextAccessor) = 
    inherit Hub()

    member private this.GetChannel() = channels.TryGetValue this.Context.ConnectionId |> snd

    override this.OnConnectedAsync() = 
        let connectionId = this.Context.ConnectionId
        let channel = createChannel connectionId (fun id x -> this.SendNotify(id, x))
        if not <| channels.TryAdd(connectionId, channel) then
            logger.LogError("Channel {0} error: can't add channel", connectionId)
            Task.CompletedTask
        else 
            logger.LogInformation("Channel {0} connected. Active connections: {x}", connectionId, channels.Count)
            base.OnConnectedAsync()

    override this.OnDisconnectedAsync(exn) =
        let connectionId = this.Context.ConnectionId

        let disconnect() =
            this.Disconnect() |> ignore
            channels.TryRemove connectionId |> ignore
            logger.LogInformation("Channel {0} disconnected. Active connections: {x}", connectionId, channels.Count)    

        let channel = this.GetChannel()
        let state = channel.GetState()
        match state with
        | Matched _ ->
            channel.Disconnect()
            addDisconnectTimer (fun () ->
                processCommand channel DisconnectCommand |> ignore
                channels.TryRemove channel.Id |> ignore
                logger.LogInformation("Removed channel {ch} due timeout", channel.Id)
            ) (TimeSpan.FromSeconds(30.0)) channel.Id
        | _ ->
            disconnect()
        base.OnDisconnectedAsync(exn)
        
    member private this.SendNotify(id, notif) =
        let client = context.GetContext<CommandProcessorHub>().Clients.Client(id)
        client.SendAsync("notification", Serializer.serializeNotify notif)
        |> ignore

    member private this.ProcessCommand request = 
         let channel = this.GetChannel()
         //logger.LogInformation(sprintf "Processing command %A" request)
         processCommand channel request |> Serializer.serializeResponse
    
    member this.Ping msg =
        this.ProcessCommand <| PingCommand {Message = msg}
         
    member this.Match optionsRaw =
        let options = Serializer.deserializeMatchOptions optionsRaw
        this.ProcessCommand <| MatchCommand options

    member this.Chat message =
        this.ProcessCommand <| ChatCommand {Message = message}

    member this.Move moveCommand = 
        let move = Serializer.deserializeMoveCommand moveCommand
        this.ProcessCommand <| MoveCommand move

    member this.Restore oldChannelId =
        let channel = this.GetChannel()
        match channel.GetState() with
        | New ->
            try
                // todo monad
                if channel.Id = oldChannelId then failwith "Invalid channel id"
                let exists, oldChannel = channels.TryGetValue oldChannelId
                exists |> checkTrue (sprintf "Channel %s does not exists" oldChannelId)
                tryRemoveDisconnectTimer oldChannel.Id |> checkTrue "Internal error 1"
                channels.TryRemove oldChannel.Id |> fst |> checkTrue "Internal error 2"
                oldChannel.Reconnect channel.Id
                channels.TryUpdate(channel.Id, oldChannel, channel) |> checkTrue "Internal error 5"
                "Ok"
            with e ->
                e.Message
        | _ -> "Only new channel can do restore"

    member this.Disconnect() =
        this.ProcessCommand DisconnectCommand