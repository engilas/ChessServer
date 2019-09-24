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

type TestNotify = Message
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
    
type Request =
| PingCommand of PingCommand
| MatchCommand
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

type ClientMessage = {
    MessageId: string
    Request: Request
}

type ResponseDto = {
    MessageId: string
    Response: Response
}

type ServerMessage = 
| Response of ResponseDto
| Notification of Notify