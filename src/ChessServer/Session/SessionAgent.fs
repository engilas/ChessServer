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
    
    let getMoveAction (x: PieceMoving) = 
        let src = positionToString x.SrcPosition
        let dst = positionToString x.DstPosition
        moveAction src dst
        
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

            let wrapParse f x source =
                try f x
                with | :? ArgumentException ->
                    replyChannel.Reply <| InvalidInput source
                    sessionError <| AgentError (sprintf "Can't parse argument %s with value %s" source x)

            let colSrc = wrapParse getColumn move.Command.From "From"
            let rowSrc = wrapParse getRow move.Command.From "From"
            let colDst = wrapParse getColumn move.Command.To "To"
            let rowDst = wrapParse getRow move.Command.To "To"

            let pieceExists = state.Engine.GetPieceTypeAt(colSrc, rowSrc) <> ChessPieceType.None

            let sameColor =
                pieceExists
                &&
                state.Engine.GetPieceColorAt(colSrc, rowSrc)
                |> fromEngineColor = state.Next

            match move.Command.PawnPromotion with
            | Some t -> state.Engine.PromoteToPieceType <- toEngineType t
            | None -> state.Engine.PromoteToPieceType <- ChessPieceType.Queen

            if
                pieceExists
                && sameColor
                && state.Engine.IsValidMove(colSrc, rowSrc, colDst, rowDst)
                && state.Engine.MovePiece(colSrc, rowSrc, colDst, rowDst)
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