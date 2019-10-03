module Hubs

open Microsoft.AspNetCore.SignalR
open FSharp.Control.Tasks.V2
open System.Threading.Tasks
open Types.Channel
open Microsoft.Extensions.Logging
open StateContainer
open CommandProcessor
open System.Collections.Concurrent

let private matcher = MatchManager.createMatcher()
let private channels = ConcurrentDictionary<string, ClientChannel>()

type CommandProcessorHub(logger: ILogger<CommandProcessorHub>) = 
    inherit Hub()

    override this.OnConnectedAsync() = 
        let userId = this.Context.ConnectionId
        let stateContainer = createStateContainer New
        let channel = {
            Id = userId
            PushNotification = fun x -> this.SendNotify(userId, x)
            ChangeState =
                fun newState ->
                    logger.LogInformation("Channel {0} changing state to {1}", userId, newState)
                    stateContainer.SetState newState
            GetState = stateContainer.GetState
        }
        if not <| channels.TryAdd(userId, channel) then
            logger.LogError("Channel {0} error: can't add channel", userId)
            Task.CompletedTask
        else 
            logger.LogInformation("Channel {0} connected", userId)
            base.OnConnectedAsync()

    override this.OnDisconnectedAsync(exn) =
        let userId = this.Context.ConnectionId
        logger.LogInformation("Channel {0} disconnected", userId)
        channels.TryRemove(userId) |> ignore
        base.OnDisconnectedAsync(exn)
        
    member private this.SendNotify(id, notif) =
        let msg = Serializer.serializeNotify notif
        this.Clients.User(id).SendAsync("notification", msg) |> ignore


    member this.Test(msg) = 
        this.Clients.All.SendAsync("bagga", msg)