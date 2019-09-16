module SessionAgent

open SessionTypes
open ChessEngine.Engine
open ChessHelper
open Types.Domain
open Types.Channel
open Types.Command
open Microsoft.Extensions.Logging
open System
open EngineMappers

[<AutoOpen>]
module private Internal =
    let logger = Logging.getLogger "SessionAgent"

let private processRegular state move (replyChannel:AsyncReplyChannel<MoveResult>) onEndGame = 
    let opponentColor =
        match state.Next with
        | White -> Black
        | Black -> White

    let opponentChannel =
        match opponentColor with
        | White -> state.WhitePlayer
        | Black -> state.BlackPlayer

    let state =
        if move.Source = state.Next then
            let src = move.Command.Src
            let dst = move.Command.Dst

            let pieceExists = state.Engine.GetPieceTypeAt(src) <> ChessPieceType.None

            let sameColor =
                pieceExists
                &&
                state.Engine.GetPieceColorAt(src)
                |> fromEngineColor = state.Next

            match move.Command.PawnPromotion with
            | Some t -> state.Engine.PromoteToPieceType <- toEngineType t
            | None -> state.Engine.PromoteToPieceType <- ChessPieceType.Queen

            if
                pieceExists
                && sameColor
                && state.Engine.IsValidMove(src, dst)
                && state.Engine.MovePiece(src, dst)
            then
                let notify = MoveNotify <| getMoveDescriptionFromEngine state.Engine
                opponentChannel.PushNotification notify
                replyChannel.Reply <| Ok ()
                {state with Next = opponentColor}
            else
                replyChannel.Reply <| Error InvalidMove
                state
        else
            replyChannel.Reply <| Error NotYourTurn
            state

    match checkEndGame state.Engine with
    | None -> state
    | Some (result, reason) ->
        onEndGame result reason
        {state with Status = Terminated}

let createSessionAgent state onEndGame = MailboxProcessor<SessionMessage>.Start(fun inbox ->
    let rec loop state = async {
        let! message = inbox.Receive()
        let nextState = 
            try 
                match message with
                | Regular (move, replyChannel) ->
                    try 
                        processRegular state move replyChannel onEndGame
                    with e ->
                        logger.LogError(e, (sprintf "Exception occurred while processing regular command. Current state: %A" state))
                        replyChannel.Reply (Error (Other "Internal error"))
                        state
                | GetState channel -> channel.Reply state; state
                | Terminate -> {state with Status = Terminated}
            with e ->
                // todo: не вылетит ли исключение при форматтинге %A (уже вылетало)
                logger.LogError(e, (sprintf "Exception occurred in agent loop. Current state: %A" state))
                state
        
        return! loop nextState
    }
    loop state
)