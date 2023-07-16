module ChessServer.Common.Types.Command

open Domain

type ConnectionId = ConnectionId of string
type MessageId = MessageId of string

type Message = { Message: string }

type PingCommand = Message
type ChatCommand = Message
type MoveCommand = { 
    Src: byte
    Dst: byte 
    PawnPromotion: PieceType option
}

type PingResult = Message
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

type ReconnectCommand = {
    OldConnectionId: ConnectionId
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
| ReconnectError of string
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
| ReconnectCommand of ReconnectCommand

type Response =
| PingResponse of PingResult
| ErrorResponse of ServerError
| OkResponse

type RequestDto = MessageId * Request

type ResponseDto = MessageId * Response

type SessionCloseReason =
| OpponentDisconnected

type Notify =
| ChatNotify of ChatNotify
| MoveNotify of MoveDescription
| EndGameNotify of EndGameNotify
| SessionStartNotify of SessionStartNotify
| SessionCloseNotify of SessionCloseReason

type ServerMessage = 
| Response of ResponseDto
| Notification of Notify