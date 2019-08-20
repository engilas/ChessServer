module CommandProcessor

open Types.Command
open Types.Channel
open Microsoft.Extensions.Logging

let private logger = Logging.getLogger("CommandProcessor")

[<AutoOpen>]
module Internal = 
    open MatchManager

    type ProcessAgentCommand =
    | Regular of Request * ClientChannel
    | ChangeState of ClientState

    type Message = ProcessAgentCommand * AsyncReplyChannel<Response option>

    let processAgent (channelId: string) initState processFun = MailboxProcessor<Message>.Start(fun inbox ->
        let rec loop state = async {
            let! command, replyChannel = inbox.Receive()

            let response, newState = 
                match command with
                | Regular (cmd, channel) -> 
                    let response = processFun cmd channel state
                    (response, state)
                | ChangeState newState ->
                    logger.LogInformation("Channel {0} changed state to {1}", channelId, newState)
                    (None, newState)

            replyChannel.Reply response
            return! loop newState
        }
        loop initState
    )

    let parseMoveError error = 
        match error with
        | NotYourTurn -> "Not your turn"
        | InvalidMove -> "Invalid move"
        | InvalidInput msg -> sprintf "Invalid input parameter: %s" msg
        | _ -> invalidArg "error" (sprintf "unknown error %A" error)

    let processCommand cmd channel (state: ClientState) = 
        let getErrorResponse msg = Some <| ErrorResponse {Message=msg}
        let changeState x = channel.ChangeState x |> ignore //avoid dead lock in mailbox
        let getInvalidStateError error =
            logger.LogInformation(sprintf "Can't process command %A for channel %s with state %A" cmd channel.Id state)
            getErrorResponse error

        try
            match cmd with
            | PingCommand ping -> 
                Some <| PingResponse {Message=ping.Message}
            | MatchCommand ->
                match state with
                | New ->
                    startMatch channel
                    changeState Matching
                    Some <| MatchResponse {Message="match started"}
                | _ -> getInvalidStateError "Already matched"
            | ChatCommand chat ->
                match state with
                | Matched session -> 
                    session.ChatMessage chat.Message
                    None
                | _ -> getInvalidStateError "Not matched"
            | MoveCommand move ->
                match state with
                | Matched session ->
                    let result = session.CreateMove move
                    match result with
                    | Ok -> None
                    | x -> getErrorResponse <| sprintf "Move error: %s" (parseMoveError x)
                | _ -> getInvalidStateError "Not matched"
            | DisconnectCommand ->
                match state with
                | Matching -> stopMatch channel
                | Matched session -> session.CloseSession "Player disconnected"
                | _ -> ()
                None
            | x ->
                failwithf "no processor for the command %A" x
                None
        with e ->
            logger.LogError(e, "Error occurred while processing command")
            getErrorResponse <| sprintf "Internal error: %s" e.Message
    

open Internal
    
let createCommandProcessor channel =
    let agent = processAgent channel New processCommand
    let postAndReply x = agent.PostAndAsyncReply(fun channel -> x, channel)

    let makeRequest request channel = (request, channel) |> (Regular >> postAndReply)
    let changeState = ChangeState >> postAndReply >> ignore

    (makeRequest, changeState)

