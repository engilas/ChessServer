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

let private matcher = MatchManager.createMatcher()
let processCommand = processCommand matcher
let private channels = ConcurrentDictionary<string, ClientChannel>()

let serializeResponse (x: Task<_>) = task {
    let! response = x
    return Serializer.serializeResponse response
}

type CommandProcessorHub(logger: ILogger<CommandProcessorHub>, context: HubContextAccessor) = 
    inherit Hub()

    override this.OnConnectedAsync() = 
        let connectionId = this.Context.ConnectionId
        let stateContainer = createStateContainer New
        let channel = {
            Id = connectionId
            PushNotification = fun x -> this.SendNotify(connectionId, x)
            ChangeState =
                fun newState ->
                    logger.LogInformation("Channel {0} changing state to {1}", connectionId, newState)
                    stateContainer.SetState newState
            GetState = stateContainer.GetState
        }
        if not <| channels.TryAdd(connectionId, channel) then
            logger.LogError("Channel {0} error: can't add channel", connectionId)
            Task.CompletedTask
        else 
            logger.LogInformation("Channel {0} connected. Active connections: {x}", connectionId, channels.Count)
            base.OnConnectedAsync()

    override this.OnDisconnectedAsync(exn) =
        let t = task {
            let connectionId = this.Context.ConnectionId
            logger.LogInformation("Channel {0} disconnected. Active connections: {x}", connectionId, channels.Count)
            let! _ = this.Disconnect()
            let _ = channels.TryRemove connectionId
            ()
            //do! base.OnDisconnectedAsync(exn)
        }
        Task.WhenAll(t :> Task, base.OnDisconnectedAsync(exn))
        //Task.CompletedTask
        //(t :> Task).ContinueWith(fun _ -> base.OnDisconnectedAsync(exn))
        
    member private this.SendNotify(id, notif) =
        let client = context.GetContext<CommandProcessorHub>().Clients.Client(id)
        client.SendAsync("notification", Serializer.serializeNotify notif)
        |> ignore

    member private this.GetChannel() = channels.TryGetValue this.Context.ConnectionId |> snd

    member private this.ProcessCommand request = 
         let channel = this.GetChannel()
         processCommand channel request |> Serializer.serializeResponse
         
    member private this.ProcessAsyncCommand request = task {
         let channel = this.GetChannel()
         let! response = processAsyncCommand matcher channel request
         return Serializer.serializeResponse response
    }
    
    member this.Ping msg =
        this.ProcessCommand <| PingCommand {Message = msg}
         
    member this.Match optionsRaw =
        let options = Serializer.deserializeMatchOptions optionsRaw
        this.ProcessAsyncCommand <| MatchCommand options

    member this.Chat message =
        this.ProcessCommand <| ChatCommand {Message = message}

    member this.Move moveCommand = 
        let move = Serializer.deserializeMoveCommand moveCommand
        this.ProcessCommand <| MoveCommand move

    member this.Disconnect() =
        this.ProcessAsyncCommand DisconnectCommand