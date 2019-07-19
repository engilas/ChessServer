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
            open System.Threading.Channels

            let logger = Logging.getLogger "MatchManager"

            let sessions = new ConcurrentDictionary<string, Color * Session>()

            type MatcherCommand =
            | Add of ClientChannel
            | Remove of ClientChannel

            let agent = MailboxProcessor.Start(fun inbox -> 
                let rec matcherLoop channels = async {
                    match channels with
                    | first::second::_ ->
                        let handleChannel channel = async {
                            let guid = Guid.NewGuid().ToString()
                            do! channel.ChangeState Matched
                            channel.PushMessage (MatchNotify {Channel=guid} |> Notify)
                            return guid
                        }
                        logger.LogInformation("Matched channels: {1}, {2}", first.Id, second.Id)
                        let! firstGuid = handleChannel first
                        let! secondGuid = handleChannel second
                        let session = createSession first second
                        sessions.TryAdd(firstGuid, (White, session)) |> ignore
                        sessions.TryAdd(secondGuid, (Black, session)) |> ignore
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
        let getChannel id =
            if matchedChannels.ContainsKey(id) then
                Some matchedChannels.[id]
            else None

        let getSession id =
            if sessions.ContainsKey(id) then
                Some sessions.[id]
            else None

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
                    | otherState ->
                        logger.LogInformation("Can't match client {0} with state {1}", channel.Id, otherState)
                        pushError "Already matched"
                | ChatCommand chat ->
                    match getChannel chat.Channel with
                    | Some otherChannel -> otherChannel.PushMessage (ChatNotify {Message=chat.Message} |> Notify)
                    | None -> pushError "Channel not found"
                | MoveCommand move ->
                    match state with
                    | Matched ->
                        match getSession move.Channel with
                        | Some (color, session) -> 
                            let! result = session.CreateMove {From = move.From; To = move.To; Source = color}
                            match result with
                            | Ok -> ()
                            | Error -> pushError "Move error"
                        | None -> pushError "Session not found"
                    | otherState ->
                        logger.LogInformation("Can't process move for client {0} with state {1}", channel.Id, otherState)
                        pushError "Not matched"
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

