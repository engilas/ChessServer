namespace ChessServer

module Session =
    open ChannelTypes
    open DomainTypes
    open CommandTypes

    module private Internal =
        open ChessHelper
        open ChessEngine.Engine

        type Message = Move * AsyncReplyChannel<MoveResult>
        type NextMove = Color

        type SessionState = {
            Engine: Engine
            WhitePlayer: ClientChannel
            BlackPlayer: ClientChannel
            Next: NextMove
        }

        let domainColorMap =
            function
            | ChessPieceColor.White -> White
            | ChessPieceColor.Black -> Black
            | x -> failwithf "unknown ChessPieceColor value %A" x
        
        let sessionAgent state = MailboxProcessor<Message>.Start(fun inbox ->
            let rec loop state = async {
                let! move, replyChannel = inbox.Receive()

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
                        | Some t -> state.Engine.PromoteToPieceType <- t
                        | None -> state.Engine.PromoteToPieceType <- ChessPieceType.Queen

                        if
                            pieceExists
                            && sameColor
                            && state.Engine.IsValidMove(colSrc, rowSrc, colDst, rowDst)
                            && state.Engine.MovePiece(colSrc, rowSrc, colDst, rowDst)
                        then
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
                            let notify = MoveNotify { Primary = move; Secondary = secondMove; TakenPiecePos = takenPiece; PawnPromotion = pawnPromoted }
                            opponentChannel.PushNotification notify
                            {state with Next = opponentColor}
                        else
                            replyChannel.Reply (Error "Invalid move")
                            state
                    else
                        replyChannel.Reply (Error "Not your turn")
                        state

                match checkEndGame state.Engine with
                | None -> return! loop state
                | Some (result, reason) -> 
                    let endGameNotify = EndGameNotify {
                        Result = result
                        Reason = reason
                    }
                    do! state.WhitePlayer.ChangeState New
                    do! state.BlackPlayer.ChangeState New
                    state.WhitePlayer.PushNotification endGameNotify
                    state.BlackPlayer.PushNotification endGameNotify

            }
            loop state
        )

    open Internal
    open ChessEngine.Engine

    let createSession whitePlayer blackPlayer =
        let engine = Engine()

        let state = {Engine = engine; WhitePlayer = whitePlayer; BlackPlayer = blackPlayer; Next = White}
        let agent = sessionAgent state
        let createMoveFun color command = 
            agent.PostAndAsyncReply(fun a -> {Source=color; Command = command}, a)

        let push channel msg = ChatNotify {Message = msg} |> channel.PushNotification

        let chatFun color msg =
            match color with
            | White -> push blackPlayer msg
            | Black -> push whitePlayer msg

        let closeFun color msg =
            let closeInternal channel = 
                channel.ChangeState New |> ignore
                SessionCloseNotify {Message = sprintf "Session closed with reason: %s" msg} |> channel.PushNotification

            match color with 
            | White -> closeInternal blackPlayer
            | Black -> closeInternal whitePlayer

        let whiteSession = {
            CreateMove = createMoveFun White
            ChatMessage = chatFun White
            CloseSession = closeFun White
        }

        let blackSession = {
            CreateMove = createMoveFun Black
            ChatMessage = chatFun Black
            CloseSession = closeFun Black
        }

        whiteSession, blackSession



