module ChessServer.CommandProcessor

open ChessServer.Common
open Types.Command
open Types.Channel
open Microsoft.Extensions.Logging
open MatchManager
open SessionTypes

let private logger = Logging.getLogger("CommandProcessor")

let getErrorResponse error errorObj = 
        logger.LogError(sprintf "Generating error response %A with error object %A" error errorObj)
        ErrorResponse error
let getInvalidStateError state error cmd id =
    logger.LogInformation(sprintf "Can't process command %A for channel %s with state %A" cmd id state)
    ErrorResponse (InvalidStateErrorResponse error)

let processCommand matcher channel cmd =
    let getErrorResponse error errorObj = 
        logger.LogError(sprintf "Generating error response %A with error object %A" error errorObj)
        ErrorResponse error
    let getInvalidStateError state error = getInvalidStateError state error cmd channel.Id
     
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
            | Matched session ->
                try
                    session.CloseSession OpponentDisconnected; OkResponse
                with SessionException SessionTerminated ->
                    logger.LogWarning("Session already closed. {id}", channel.Id)
                    OkResponse
            | _ -> OkResponse
    with e ->
        logger.LogError(e, "Error occurred while processing command")
        getErrorResponse InternalErrorResponse e.Message
    
