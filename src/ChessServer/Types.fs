namespace ChessServer

module Types =
    type PingCommand = { Message:string }
    type ChatCommand = { Message:string; Channel:string }

    type PingResponse = { Message:string }
    type MatchResponse = { Message:string }

    type TestNotify = { Message:string }
    type ChatNotify = { Message:string }
    type MatchNotify = { Channel:string }

    type ErrorResponse = { Message:string }
    type ErrorNotify = ErrorResponse
    

    type Request =
    | PingCommand of PingCommand
    | MatchCommand
    | ChatCommand of ChatCommand

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

    type ServerMessage =
    | Response of Response
    | Notify of Notify