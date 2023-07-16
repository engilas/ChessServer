module ChessServer.ClientChannelManager

open StateContainer
open ChessServer.Common
open Types.Channel
open Types.Command
open Microsoft.Extensions.Logging

let private logger = Logging.getLogger "ClientChannelManager"

type private ChannelInternalState = {
    Id: ConnectionId
    Disconnected: bool
    DisconnectedNotifications: Notify list
    ClientState: ClientState
}

let createChannel id notify =
    let state = {
        Id = id
        Disconnected = false
        DisconnectedNotifications = []
        ClientState = New
    }

    let stateContainer = createStateContainer state
    let getState = stateContainer.GetState
    //let getId() = getState().Id
    let isDisconnected() = getState().Disconnected
    let getClientState() = stateContainer.GetState().ClientState
    let setClientState x = stateContainer.UpdateState (fun s -> {s with ClientState = x})
    let pushState x = 
        stateContainer.UpdateState (fun s -> {s with DisconnectedNotifications = x :: s.DisconnectedNotifications })

    let channel = {
        Id = id
        PushNotification =
            fun x ->
                if isDisconnected() then 
                    pushState x
                else notify x
        ChangeState =
            fun newState ->
                logger.LogInformation("Channel {0} changing state to {1}", id, newState)
                setClientState newState
        GetState = getClientState
        IsDisconnected = isDisconnected
        Disconnect = fun () ->
            if isDisconnected() then
                invalidOp "Already disconnected"
            stateContainer.UpdateState (fun s -> {s with Disconnected = true})
        Reconnect = fun newId ->
            if not <| isDisconnected() then invalidOp "Can reconnect only disconnected channel"
            stateContainer.UpdateState (fun s -> {s with Id = newId; Disconnected = false})
            let notifications = getState().DisconnectedNotifications
            stateContainer.UpdateState (fun s -> {s with DisconnectedNotifications = []})
            notifications
            |> List.rev
            |> List.iter notify // todo notify newid ??
    }
    channel

        