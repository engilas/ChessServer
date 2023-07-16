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
    logger.LogInformation(sprintf "Can't process command %A for channel %A with state %A" cmd id state)
    ErrorResponse (InvalidStateErrorResponse error)

let processCommand matcher channelManager channel cmd =
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
        | ReconnectCommand args ->
            let getError = ReconnectError >> ErrorResponse
            let newConnectionId = channel.Id
            match channel.GetState() with
            | New ->
                try
                    // todo monad
                    let defaultMsg = "Invalid channel"
                    if newConnectionId = args.OldConnectionId then failwith defaultMsg
                    match channelManager.Get args.OldConnectionId with 
                    | Some oldChannel ->
                        channelManager.RemoveDisconnectTimeout oldChannel.Id
                        channelManager.Remove oldChannel.Id
                        oldChannel.Reconnect newConnectionId
                        //channels.TryUpdate(newConnectionId, oldChannel, channel) |> checkTrue "Internal error 2"
                        channelManager.Add oldChannel // ?? wtf
                        OkResponse
                    | None ->
                        failwith defaultMsg
                    //exists |> checkTrue defaultMsg
                    //tryRemoveDisconnectTimer oldConnectionId |> checkTrue defaultMsg
                    //channels.TryRemove oldConnectionId |> fst |> checkTrue "Internal error 1"
                    //oldChannel.Reconnect newConnectionId
                    //channels.TryUpdate(newConnectionId, oldChannel, channel) |> checkTrue "Internal error 2"
                    //OkResponse
                with e ->
                    getError e.Message
            | _ -> getError "Only new channel can do restore"
            //|> Serializer.serializeResponse
    with e ->
        logger.LogError(e, "Error occurred while processing command")
        getErrorResponse InternalErrorResponse e.Message
    
