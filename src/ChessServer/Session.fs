namespace ChessServer

module Session =
    open ChannelTypes
    open CommandTypes

    module private Internal =

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

                let state =
                    if move.Source = state.Next then
                        replyChannel.Reply Ok
                        let notify = MoveNotify {From = move.From; To = move.To} |> Notify
                        opponentChannel.PushMessage notify
                        {state with Next = opponentColor}
                    else
                        replyChannel.Reply Error
                        state

                return! loop state
            }
            loop state
        )

    open Internal

    let createSession whitePlayer blackPlayer =
        let state = {WhitePlayer = whitePlayer; BlackPlayer = blackPlayer; Next = White}
        let agent = sessionAgent state
        let createMoveFun move = agent.PostAndAsyncReply(fun a -> move, a)

        let push channel msg = ChatNotify {Message = msg} |> Notify |> channel.PushMessage

        let chatFun color msg =
            match color with
            | White -> push blackPlayer msg
            | Black -> push whitePlayer msg

        {
            CreateMove = createMoveFun
            ChatMessage = chatFun
        }



