namespace ChessServer

module Session =
    type Color = White | Black
    type Move = {
        Source: Color
        From: string
        To: string
    }
    type MoveResult = Ok | Error

    type Session = {
        CreateMove: Move -> Async<MoveResult>
    }

    module private Internal =
        open ChannelTypes
        open CommandTypes

        type Message = Move * AsyncReplyChannel<MoveResult>
        type NextMove = Color

        type SessionState = {
            WhitePlayer: ClientChannel
            BlackPlayer: ClientChannel
            Next: NextMove
        }
        
        let sessionAgent state = MailboxProcessor<Message>.Start(fun inbox ->
            let rec loop state = async {
                let! (move, replyChannel) = inbox.Receive()

                let opponentColor =
                    match state.Next with
                    | White -> Black
                    | Black -> White

                let opponentChannel =
                    match opponentColor with
                    | White -> state.WhitePlayer
                    | Black -> state.BlackPlayer

                if move.Source = state.Next then
                    replyChannel.Reply Ok
                    let notify = MoveNotify {From = move.From; To = move.To} |> Notify
                    opponentChannel.PushMessage notify
                else
                    replyChannel.Reply Error

                return! loop {state with Next = opponentColor}
            }
            loop state
        )

    open Internal

    let createSession whitePlayer blackPlayer =
        let state = {WhitePlayer = whitePlayer; BlackPlayer = blackPlayer; Next = White}
        let agent = sessionAgent state
        let createMoveFun move = agent.PostAndAsyncReply(fun a -> move, a)
        {CreateMove = createMoveFun}



