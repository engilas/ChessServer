module SessionAgent

open SessionTypes
open ChessEngine.Engine
open Helper
open ChessHelper
open Types.Domain
open Types.Channel
open Types.Command
open Types

[<AutoOpen>]
module private Internal =
    //todo test mappers
    let domainColorMap = function
    | ChessPieceColor.White -> White
    | ChessPieceColor.Black -> Black
    | x -> invalidArg "arg" x "unknown ChessPieceColor value"

    let typeMap fst snd x = 
        [
            ChessPieceType.King, King
            ChessPieceType.Queen, Queen
            ChessPieceType.Rook, Rook
            ChessPieceType.Bishop, Bishop
            ChessPieceType.Knight, Knight
            ChessPieceType.Pawn, Pawn
        ] |> List.find (fst >> ((=) x)) |> snd

    let fromEngineType = function
    | None -> None
    | Some x -> x |> function
        | ChessPieceType.None -> None
        | x -> x |> typeMap fst snd |> Some

    let toEngineType = typeMap snd fst

let private processRegular state move (replyChannel:AsyncReplyChannel<MoveResult>) = 
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

            let colSrc = getColumn move.Command.From
            let rowSrc = getRow move.Command.From
            let colDst = getColumn move.Command.To
            let rowDst = getRow move.Command.To

            let pieceExists = state.Engine.GetPieceTypeAt(colSrc, rowSrc) <> ChessPieceType.None

            let sameColor =
                pieceExists
                &&
                state.Engine.GetPieceColorAt(colSrc, rowSrc)
                |> domainColorMap = state.Next

            match move.Command.PawnPromotion with
            | Some t -> state.Engine.PromoteToPieceType <- toEngineType t
            | None -> state.Engine.PromoteToPieceType <- ChessPieceType.Queen

            if
                pieceExists
                && sameColor
                && state.Engine.IsValidMove(colSrc, rowSrc, colDst, rowDst)
                && state.Engine.MovePiece(colSrc, rowSrc, colDst, rowDst)
            then
                //todo попробоовать избавиться от лямбды getresult - сразу давать значение
                let ifExists (x:ChessPieceType) getResult =
                    match x with
                    | ChessPieceType.None -> None
                    | _ -> Some <| getResult()

                let lastMove = state.Engine.LastMove
                let takenPiece = 
                    ifExists lastMove.TakenPiece.PieceType (fun () -> getPosition lastMove.TakenPiece.Position)

                let getMoveAction (x: PieceMoving) = 
                    let src = getPosition x.SrcPosition
                    let dst = getPosition x.DstPosition
                    moveAction src dst
                let move = getMoveAction lastMove.MovingPiecePrimary
                let secondMove = 
                    ifExists lastMove.MovingPieceSecondary.PieceType (fun () -> getMoveAction lastMove.MovingPieceSecondary)

                let pawnPromoted = ifExists lastMove.PawnPromotedTo (fun () -> lastMove.PawnPromotedTo)

                replyChannel.Reply Ok
                let notify = MoveNotify { Primary = move; Secondary = secondMove; TakenPiecePos = takenPiece; PawnPromotion = fromEngineType pawnPromoted }
                opponentChannel.PushNotification notify
                {state with Next = opponentColor}
            else
                replyChannel.Reply (Error "Invalid move")
                state
        else
            replyChannel.Reply (Error "Not your turn")
            state

    match checkEndGame state.Engine with
    | None -> state
    | Some (result, reason) -> 
        let endGameNotify = EndGameNotify {
            Result = result
            Reason = reason
        }
        state.WhitePlayer.ChangeState New
        state.BlackPlayer.ChangeState New
        state.WhitePlayer.PushNotification endGameNotify
        state.BlackPlayer.PushNotification endGameNotify
        {state with Status = Terminated}

let sessionAgent state = MailboxProcessor<SessionMessage>.Start(fun inbox ->
    let rec loop state = async {
        let! message = inbox.Receive()

        let nextState = 
            match message with
            | Regular (move, replyChannel) ->
                processRegular state move replyChannel
            | GetState channel -> channel.Reply state; state
            | Terminate -> {state with Status = Terminated}
        
        return! loop nextState
    }
    loop state
)