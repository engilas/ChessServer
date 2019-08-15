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
                    let! channels = async {
                        match channels with
                        | first::second::lst -> 
                            logger.LogInformation("Matched channels: {1}, {2}", first.Id, second.Id)
                            let whiteSession, blackSession = createSession first second
                            do! first.ChangeState (Matched whiteSession)
                            do! second.ChangeState (Matched blackSession)
                            let notify = MatchNotify {Message = "matched"}
                            first.PushNotification notify
                            second.PushNotification notify
                            return lst
                        | lst -> return lst
                    }

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
        let stopMatch x = Remove x |> agent.Post

    module Internal = 
        open MatchManager

        type ProcessAgentCommand =
        | Regular of Request * ClientChannel
        | ChangeState of ClientState

        type Message = ProcessAgentCommand * AsyncReplyChannel<Response option>

        let processAgent (channelId: string) initState processFun = MailboxProcessor<Message>.Start(fun inbox ->
            let rec loop state = async {
                let! command, replyChannel = inbox.Receive()

                let! response, newState = async {
                    match command with
                    | Regular (cmd, channel) -> 
                        let! response = processFun cmd channel state
                        return (response, state)
                    | ChangeState newState ->
                        logger.LogInformation("Channel {0} changed state to {1}", channelId, newState)
                        return (None, newState)
                }

                replyChannel.Reply response
                return! loop newState
            }
            loop initState
        )

        let processCommand cmd channel (state: ClientState) = async {
            let getErrorResponse msg = Some <| ErrorResponse {Message=msg}
            let changeState x = channel.ChangeState x |> ignore //avoid dead lock in mailbox
            let getInvalidStateError error =
                logger.LogInformation(sprintf "Can't process command %A for channel %s with state %A" cmd channel.Id state)
                getErrorResponse error

            try
                match cmd with
                | PingCommand ping -> 
                    return Some <| PingResponse {Message=ping.Message}
                | MatchCommand ->
                    match state with
                    | New ->
                        startMatch channel
                        changeState Matching
                        return Some <| MatchResponse {Message="match started"}
                    | _ -> return getInvalidStateError "Already matched"
                | ChatCommand chat ->
                    match state with
                    | Matched session -> 
                        session.ChatMessage chat.Message
                        return None
                    | _ -> return getInvalidStateError "Not matched"
                | MoveCommand move ->
                    match state with
                    | Matched session ->
                        let! result = session.CreateMove move
                        match result with
                        | Ok -> return None
                        | Error msg -> return getErrorResponse <| sprintf "Move error: %s" msg
                    | _ -> return getInvalidStateError "Not matched"
                | DisconnectCommand ->
                    match state with
                    | Matching -> stopMatch channel
                    | Matched session -> session.CloseSession "Player disconnected"
                    return None
                | x ->
                    failwithf "no processor for the command %A" x
                    return None
            with e ->
                logger.LogError(e, "Error occurred while processing command")
                return getErrorResponse <| sprintf "Internal error: %s" e.Message
        }

    open Internal
    
    let createCommandProcessor channel =
        let agent = processAgent channel New processCommand
        let postAndReply x = agent.PostAndAsyncReply(fun channel -> x, channel)
        let f1 request channel = (request, channel) |> Regular |> postAndReply
        let f2 state = state |> ChangeState |> postAndReply |> Async.Ignore

        (f1, f2)

