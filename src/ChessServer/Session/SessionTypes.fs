module SessionTypes

open Types.Channel
open ChessEngine.Engine
open Types.Domain

type SessionError =
    | SessionTerminated
    | AgentError of string

exception SessionException of SessionError

let sessionError error = raise (SessionException error)

type SessionStatus = Active | Terminated

type SessionState = {
    Engine: Engine
    WhitePlayer: ClientChannel
    BlackPlayer: ClientChannel
    Next: Color
    Status: SessionStatus
}

type SessionMessage =
    | Regular of Move * AsyncReplyChannel<MoveResult>
    | GetState of AsyncReplyChannel<SessionState>
    | Terminate