module CommandProcessor

open Types.Command
open Types.Channel
open Microsoft.Extensions.Logging
open MatchManager

let private logger = Logging.getLogger("CommandProcessor")

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
        | MatchCommand options ->
            match channel.GetState() with
            | New ->
                match matcher.StartMatch channel options with
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
    with e ->
        logger.LogError(e, "Error occurred while processing command")
        getErrorResponse InternalErrorResponse e.Message
    

