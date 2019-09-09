module SessionTypes

open Types.Channel
open ChessEngine.Engine
open Types.Domain
open Types.Command

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

type SessionMove = {
    Source: Color
    Command: MoveCommand
}

type SessionMessage =
    | Regular of SessionMove * AsyncReplyChannel<MoveResult>
    | GetState of AsyncReplyChannel<SessionState>
    | Terminate