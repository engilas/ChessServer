namespace ChessServer

module CommandProcessor =
    open CommandTypes
    open ChannelTypes
    open System
    open System.Collections.Concurrent
    open Microsoft.Extensions.Logging
    open Session

    let private logger = Logging.getLogger("CommandProcessor")

    module private MatchManager = 
        module private Internal =
            let logger = Logging.getLogger "MatchManager"

            type MatcherCommand =
            | Add of ClientChannel
            | Remove of ClientChannel

            let agent = MailboxProcessor.Start(fun inbox -> 
                let rec matcherLoop channels = async {
                    match channels with
                    | first::second::_ ->
                        logger.LogInformation("Matched channels: {1}, {2}", first.Id, second.Id)
                        let session = createSession first second
                        let state color = Matched (color, session)
                        do! first.ChangeState (state White)
                        do! second.ChangeState (state Black)
                        let notify = MatchNotify {Message = "matched"} |> Notify
                        first.PushMessage notify
                        second.PushMessage notify
                    | _ -> ()
                    let! command = inbox.Receive()

                    let list =
                        match command with
                        | Add channel -> channel :: channels
                        | Remove channel -> channels |> List.filter (fun x -> x.Id <> channel.Id)

                    return! matcherLoop list
                }
                matcherLoop []
            )

            

        open Internal

        let startMatch x = Add x |> agent.Post

    module Internal = 
        open MatchManager

        type ProcessAgentCommand =
        | Regular of Request * ClientChannel
        | ChangeState of ClientState

        type Message = ProcessAgentCommand * AsyncReplyChannel<unit>

        let processAgent (channelId: string) initState processFun = MailboxProcessor<Message>.Start(fun inbox ->
            let rec loop state = async {
                let! command, replyChannel = inbox.Receive()

                let! newState = async {
                    match command with
                    | Regular (cmd, channel) -> 
                        do! processFun cmd channel state
                        return state
                    | ChangeState newState ->
                        logger.LogInformation("Channel {0} changed state to {1}", channelId, newState)
                        return newState
                }

                replyChannel.Reply()
                return! loop newState
            }
            loop initState
        )

        let processCommand cmd channel (state: ClientState) = async {
            let pushError msg = channel.PushMessage (ErrorResponse {Message=msg} |> Response)
            let changeState x = channel.ChangeState x |> ignore //avoid dead lock in mailbox
            let invalidState error =
                logger.LogInformation(sprintf "Can't process command %A for channel {0} with state {1}" cmd, channel.Id, state)
                pushError error

            try
                match cmd with
                | PingCommand ping -> 
                    let pong = PingResponse {Message=ping.Message} |> Response
                    channel.PushMessage pong
                | MatchCommand ->
                    match state with
                    | New ->
                        startMatch channel
                        let response = MatchResponse {Message="match started"} |> Response
                        channel.PushMessage response
                        changeState Matching
                    | _ -> invalidState "Already matched"
                | ChatCommand chat ->
                    match state with
                    | Matched (color, session) -> 
                        session.ChatMessage color chat.Message
                    | _ -> invalidState "Not matched"
                | MoveCommand move ->
                    match state with
                    | Matched (color, session) ->
                        let! result = session.CreateMove {From = move.From; To = move.To; Source = color}
                        match result with
                        | Ok -> ()
                        | Error -> pushError "Move error"
                    | _ -> invalidState "Not matched"
                | x ->
                    failwithf "no processor for the command %A" x
            with e ->
                logger.LogError(e, "Error occurred while processing command")
                let errResponse = (ErrorResponse {Message = sprintf "Internal error: %s" e.Message}) |> Response
                channel.PushMessage errResponse
        }

    open Internal
    

    let createCommandProcessor channel =
        let agent = processAgent channel New processCommand
        let postAndReply x = agent.PostAndAsyncReply(fun channel -> x, channel)
        let f1 request channel = (request, channel) |> Regular |> postAndReply
        let f2 state = state |> ChangeState |> postAndReply

        (f1, f2)

