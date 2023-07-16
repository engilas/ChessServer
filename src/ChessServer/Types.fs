module ChessServer.Types.Channel

open ChessServer.Common
open Types.Command

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


// todo отвязаться от signalr id
// при реконнекте conn id в хабе меняется, ClientChannel.Id остается прежним

type ClientChannel = {
    Id: ConnectionId
    PushNotification: Notify -> unit
    ChangeState: ClientState -> unit
    GetState: unit -> ClientState
    IsDisconnected: unit -> bool
    Disconnect: unit -> unit
    Reconnect: ConnectionId -> unit
}

type ChannelManager = {
    Add: ClientChannel -> unit
    Get: ConnectionId -> Option<ClientChannel>
    Remove: ConnectionId -> unit
    Count: unit -> int
    AddDisconnectTimeout: (unit -> unit) -> ConnectionId -> unit
    RemoveDisconnectTimeout: ConnectionId -> unit
}