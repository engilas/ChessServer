module CommandProcessor

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

let processAsyncCommand matcher channel cmd = async {
    let getInvalidStateError state error = getInvalidStateError state error cmd channel.Id
    
    try 
        match cmd with
        | MatchCommand options ->
            match channel.GetState() with
            | New ->
                let! result = matcher.StartMatch channel options
                match result with
                | Ok _ -> return OkResponse
                | Error e -> return getErrorResponse MatchingErrorResponse e
            | x -> return getInvalidStateError x "Already matched"
        | DisconnectCommand ->
            match channel.GetState() with
            | Matching ->
                let! result = matcher.StopMatch channel
                match result with
                | Ok _ -> return OkResponse
                | Error e -> return getErrorResponse MatchingErrorResponse e
            | Matched session ->
                try
                    session.CloseSession OpponentDisconnected
                    return OkResponse
                with SessionException SessionTerminated ->
                    logger.LogWarning("Session already closed. {id}", channel.Id)
                    return OkResponse
            | _ -> return OkResponse
        | x -> return failwithf "Invalid async command %A" x
        with e ->
            logger.LogError(e, "Error occurred while processing command")
            return getErrorResponse InternalErrorResponse e.Message
}

let processCommand matcher channel cmd =
    let getErrorResponse error errorObj = 
        logger.LogError(sprintf "Generating error response %A with error object %A" error errorObj)
        ErrorResponse error
    let getInvalidStateError state error = getInvalidStateError state error cmd channel.Id
     
    try
        match cmd with
        | PingCommand ping -> PingResponse {Message=ping.Message}
//        | MatchCommand options ->
//            match channel.GetState() with
//            | New ->
//                match matcher.StartMatch channel options with
//                | Ok _ -> OkResponse
//                | Error e -> getErrorResponse MatchingErrorResponse e
//            | x -> getInvalidStateError x "Already matched"
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
        | x -> failwithf "Invalid sync command %A" x
    with e ->
        logger.LogError(e, "Error occurred while processing command")
        getErrorResponse InternalErrorResponse e.Message
    

