namespace ChessServer

module CommandTypes =
    type Message = { Message:string }

    type PingCommand = Message
    type ChatCommand = { Message:string; Channel:string }
    type MoveCommand = { Channel: string; From: string; To: string }

    type PingResponse = Message
    type MatchResponse = Message

    type TestNotify = Message
    type ChatNotify = Message
    type MatchNotify = { Channel:string }
    type MoveNotify = { From: string; To: string }

    type ErrorResponse = Message
    type ErrorNotify = ErrorResponse
    
    type Request =
    | PingCommand of PingCommand
    | MatchCommand
    | ChatCommand of ChatCommand
    | MoveCommand of MoveCommand

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

    type ServerMessage =
    | Response of Response
    | Notify of Notify

module ChannelTypes = 
    open CommandTypes

    type PushMessage = ServerMessage -> unit

    type ClientState = 
    | New
    | Matching
    | Matched

    type ClientChannel = {
        Id: string
        PushMessage: PushMessage
        ChangeState: ClientState -> Async<unit>
    }