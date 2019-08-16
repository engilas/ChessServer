module SessionTypes

open Types.Channel
open ChessEngine.Engine
open Types.Domain

type SessionMessage =
    | Regular of Move * AsyncReplyChannel<MoveResult>
    | Terminate

type SessionStatus = Active | Terminated

type SessionState = {
    Engine: Engine
    WhitePlayer: ClientChannel
    BlackPlayer: ClientChannel
    Next: Color
    Status: SessionStatus
}