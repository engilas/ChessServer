namespace ChessServer

module CommandTypes =
    type Message = { Message:string }

    type PingCommand = Message
    type ChatCommand = Message
    type MoveCommand = { From: string; To: string }

    type PingResponse = Message
    type MatchResponse = Message

    type TestNotify = Message
    type ChatNotify = Message
    type MatchNotify = Message
    type MoveNotify = { From: string; To: string }

    type ErrorResponse = Message
    type ErrorNotify = ErrorResponse
    
    type Request =
    | PingCommand of PingCommand
    | MatchCommand
    | ChatCommand of ChatCommand
    | MoveCommand of MoveCommand
    | DisconnectCommand

    type Response =
    | PingResponse of PingResponse
    | MatchResponse of MatchResponse
    | ErrorResponse of ErrorResponse

    type Method = string

    type Notify =
    | TestNotify of TestNotify
    | ErrorNotify of Method * ErrorNotify
    | MatchNotify of MatchNotify
    | ChatNotify of ChatNotify
    | MoveNotify of MoveNotify
    | SessionCloseNotify of Message

module ChannelTypes = 
    open CommandTypes

    type Color = White | Black
    type Move = {
        Source: Color
        From: string
        To: string
    }
    type MoveResult = Ok | Error of string

    type Session = {
        CreateMove: string -> string -> Async<MoveResult>
        ChatMessage: string -> unit
        CloseSession: string -> unit
    }

    type ClientState = 
    | New
    | Matching
    | Matched of Session

    type ClientChannel = {
        Id: string
        PushNotification: Notify -> unit
        ChangeState: ClientState -> Async<unit>
    }