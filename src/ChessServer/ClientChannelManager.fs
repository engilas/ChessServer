module ClientChannelManager

open StateContainer
open Types.Channel
open Microsoft.Extensions.Logging

let private logger = Logging.getLogger "ClientChannelManager"

let createChannel id notify =
    let stateContainer = createStateContainer New
    let notificationQueue = createStateHistoryContainer()
    let getState = stateContainer.GetState
    let setState = stateContainer.SetState
    let channel = {
        Id = id
        // todo flush notif
        PushNotification =
            fun x ->
                match getState() with
                | Disconnected _ ->
                    notificationQueue.PushState x
                | _ ->
                    let rec getNotifications() = seq {
                        match notificationQueue.PopState() with
                        | Some x -> yield x; yield! getNotifications()
                        | None -> ()
                    }
                    getNotifications()
                    |> List.ofSeq
                    |> List.rev
                    |> List.iter (notify id)
                    notify id x
        ChangeState =
            fun newState ->
                logger.LogInformation("Channel {0} changing state to {1}", id, newState)
                setState newState
        GetState =
            fun () ->
                match getState() with
                | Disconnected state -> state
                | x -> x
        IsDisconnected = fun () ->
                match getState() with
                | Disconnected _ -> true
                | _ -> false
    }
    channel
    
let disconnectChannel channel =
    let currentState = channel.GetState()
    channel.ChangeState <| Disconnected currentState
    
let replaceWithNewChannel channel newId =
    if not <| channel.IsDisconnected() then invalidOp "Can replace only disconnected channel"
    match channel.GetState() with
    | state ->
        // in fact there is (Disconnected x) state
        channel.ChangeState state
        {channel with Id = newId}
        