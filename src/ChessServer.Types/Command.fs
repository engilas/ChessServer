module Types.Command

open Domain

type Message = { Message: string }

type PingCommand = Message
type ChatCommand = Message
type MoveCommand = { 
    Src: byte
    Dst: byte 
    PawnPromotion: PieceType option
}

type PingResponse = Message
type MatchResponse = Message

type ChatNotify = Message
type SessionStartNotify = {
    Color: Color
}

type SessionResult = WhiteWin | BlackWin | Draw

type EndGameNotify = {
    Result: SessionResult
    Reason: string
}

type MoveError =
| NotYourTurn
| InvalidMove
| InvalidInput of string
| Other of string

type ServerError =
| InvalidStateErrorResponse of string
| MatchingErrorResponse
| MoveErrorResponse of MoveError
| InternalErrorResponse
    
type MatchOptions = {
    Group: string option
}
    
type Request =
| PingCommand of PingCommand
| MatchCommand of MatchOptions
| ChatCommand of ChatCommand
| MoveCommand of MoveCommand
| DisconnectCommand

type Response =
| PingResponse of PingResponse
| ErrorResponse of ServerError
| OkResponse

type SessionCloseReason =
| OpponentDisconnected

type Notify =
| ChatNotify of ChatNotify
| MoveNotify of MoveDescription
| EndGameNotify of EndGameNotify
| SessionStartNotify of SessionStartNotify
| SessionCloseNotify of SessionCloseReason