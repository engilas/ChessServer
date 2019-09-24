module CommandProcessor

open Types.Command
open Types.Channel
open Microsoft.Extensions.Logging
open MatchManager
open StateContainer

let private logger = Logging.getLogger("CommandProcessor")

[<AutoOpen>]
module Internal = 

    type Message = Request * AsyncReplyChannel<Response>

    let processAgent processFun = MailboxProcessor<Message>.Start(fun inbox ->
        let rec loop() = async {
            let! command, replyChannel = inbox.Receive()
            let response = processFun command
            replyChannel.Reply response
            return! loop()
        }
        loop()
    )

    let processCommand matcher channel cmd =
        let getErrorResponse error errorObj = 
            logger.LogError(sprintf "Generating error response %A with error object %A" error errorObj)
            ErrorResponse error
        let getInvalidStateError state error =
            logger.LogInformation(sprintf "Can't process command %A for channel %s with state %A" cmd channel.Id state)
            ErrorResponse (InvalidStateErrorResponse error)
         
        try
            match cmd with
            | PingCommand ping -> PingResponse {Message=ping.Message}
            | MatchCommand ->
                match channel.GetState() with
                | New ->
                    match matcher.StartMatch channel with
                    | Ok _ -> OkResponse
                    | Error e -> getErrorResponse MatchingErrorResponse e
                | x -> getInvalidStateError x "Already matched"
            | ChatCommand chat ->
                match channel.GetState() with
                | Matched session -> 
                    session.ChatMessage chat.Message
                    OkResponse
                | x -> getInvalidStateError x "Not matched"
            | MoveCommand move ->
                match channel.GetState() with
                | Matched session ->
                    let result = session.CreateMove move
                    match result with
                    | Ok _ -> OkResponse
                    | Error x -> getErrorResponse (MoveErrorResponse x) ""
                | x -> getInvalidStateError x "Not matched"
            | DisconnectCommand ->
                match channel.GetState() with
                | Matching ->
                    match matcher.StopMatch channel with
                    | Ok _ -> OkResponse
                    | Error e -> getErrorResponse MatchingErrorResponse e
                | Matched session -> session.CloseSession OpponentDisconnected; OkResponse
                | _ -> OkResponse
            | x ->
                failwithf "no processor for the command %A" x
        with e ->
            logger.LogError(e, "Error occurred while processing command")
            getErrorResponse InternalErrorResponse e.Message
    
let createCommandProcessor channel (matcher: Matcher) =
    let agent = processAgent <| processCommand matcher channel
    fun x -> agent.PostAndAsyncReply(fun channel -> x, channel)
    

