﻿module SessionAgent

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
    
    let getMoveAction (x: PieceMoving) = moveAction x.SrcPosition x.DstPosition
        
    let ifExists (x:ChessPieceType) value =
        match x with
        | ChessPieceType.None -> None
        | _ -> Some value

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
                let lastMove = state.Engine.LastMove
                let takenPiece = ifExists lastMove.TakenPiece.PieceType
                                 <| positionToString lastMove.TakenPiece.Position
                    
                let move = getMoveAction lastMove.MovingPiecePrimary
                let secondMove = ifExists lastMove.MovingPieceSecondary.PieceType
                                 <| getMoveAction lastMove.MovingPieceSecondary
                let pawnPromoted = ifExists lastMove.PawnPromotedTo lastMove.PawnPromotedTo

                replyChannel.Reply Ok
                let notify = MoveNotify { Primary = move; Secondary = secondMove; TakenPiecePos = takenPiece; PawnPromotion = fromEngineType pawnPromoted }
                opponentChannel.PushNotification notify
                {state with Next = opponentColor}
            else
                replyChannel.Reply InvalidMove
                state
        else
            replyChannel.Reply NotYourTurn
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
                    processRegular state move replyChannel onEndGame
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