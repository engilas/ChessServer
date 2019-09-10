namespace Types

module Domain =
    type Color = White | Black
    type SessionResult = WhiteWin | BlackWin | Draw
    type PieceType =
    | King
    | Queen
    | Rook
    | Bishop
    | Knight
    | Pawn

    type Move = {
        Src: byte
        Dst: byte
    }

    type MoveDescription = { 
        Primary: Move
        Secondary: Move option
        TakenPiecePos: string option
        PawnPromotion: PieceType option
        Check: bool
        Mate: bool
    }

module Command =
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
        FirstMove: Color
    }

    type EndGameNotify = {
        Result: SessionResult
        Reason: string
    }

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
    | ChatNotify of ChatNotify
    | MoveNotify of MoveDescription
    | EndGameNotify of EndGameNotify
    | SessionStartNotify of SessionStartNotify
    | SessionCloseNotify of Message

module Channel =
    open Command

    type MoveResult = 
    | Ok
    | NotYourTurn
    | InvalidMove
    | InvalidInput of string
    | Other of string

    type Session = {
        CreateMove: MoveCommand -> MoveResult
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
        ChangeState: ClientState -> unit
    }