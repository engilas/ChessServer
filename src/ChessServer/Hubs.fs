module Hubs

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

let private matcher = MatchManager.createMatcher()
let processCommand = processCommand matcher
let private channels = ConcurrentDictionary<string, ClientChannel>()

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
            logger.LogInformation("Channel {0} connected", connectionId)
            base.OnConnectedAsync()

    override this.OnDisconnectedAsync(exn) =
        let connectionId = this.Context.ConnectionId
        logger.LogInformation("Channel {0} disconnected", connectionId)
        let (_, channel) = channels.TryRemove connectionId
        processCommand channel DisconnectCommand |> ignore
        base.OnDisconnectedAsync(exn)
        
    member private this.SendNotify(id, notif) =
        let client = context.GetContext<CommandProcessorHub>().Clients.Client(id)
        client.SendAsync("notification", Serializer.serializeNotify notif)
        |> ignore

    member private this.GetChannel() = channels.TryGetValue this.Context.ConnectionId |> snd

    member private this.ProcessCommand request = 
         let channel = this.GetChannel()
         processCommand channel request |> Serializer.serializeResponse

    
    member this.Match(group: string) =
        let group = if System.String.IsNullOrWhiteSpace(group) then None else Some group
        this.ProcessCommand <| MatchCommand {Group = group}

    member this.Chat message =
        this.ProcessCommand <| ChatCommand {Message = message}

    member this.Move moveCommand = 
        let move = Serializer.deserializeMoveCommand moveCommand
        this.ProcessCommand <| MoveCommand move

    member this.Disconnect() =
        this.ProcessCommand DisconnectCommand