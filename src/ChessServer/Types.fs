﻿module Types.Channel

open Command

type MoveResult = Result<unit, MoveError>

type Session = {
    CreateMove: MoveCommand -> MoveResult
    ChatMessage: string -> unit
    CloseSession: SessionCloseReason -> unit
}

type ClientState = 
| New
| Matching
| Matched of Session

type ClientChannel = {
    Id: string
    PushNotification: Notify -> unit
    ChangeState: ClientState -> unit
    GetState: unit -> ClientState
    IsDisconnected: unit -> bool
    Disconnect: unit -> unit
    Reconnect: string -> unit
}