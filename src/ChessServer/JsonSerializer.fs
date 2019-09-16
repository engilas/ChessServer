﻿module JsonSerializer 

open Types.Command
open Helper
open System.Runtime.Serialization
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open ChessServer

[<AutoOpen>]
module private Internal =

    [<DataContract>]
    [<CLIMutable>]
    type JsonRpcRequest = {
        [<field: DataMember(Name="method") >]
        Method: string
        [<field: DataMember(Name="params") >]
        Params: JObject
        [<field: DataMember(Name="id") >]
        Id: int
    }

    [<DataContract>]
    type JsonRpcResponse<'a, 'err> = {
        [<field: DataMember(Name="result") >]
        Result: 'a option
        [<field: DataMember(Name="error") >]
        Error: 'err option
        [<field: DataMember(Name="id") >]
        Id: int
    }

    [<DataContract>]
    type JsonRpcNotify<'a, 'err>  = {
        [<field: DataMember(Name="method") >]
        Method: string
        [<field: DataMember(Name="params") >]
        Params: 'a option
        [<field: DataMember(Name="error") >]
        Error: 'err option
    }

    let createNotify method param = {Method = method; Params = Some param; Error = None}
    let createErrorNotify method error = {Method = method; Params = None; Error = Some error}
    let createResponse id result = {Result = Some result; Id = id; Error = None}
    let createErrorResponse id error = {Result = None; Id = id; Error = Some error}

    let serialize obj = 
        let settings = new JsonSerializerSettings()
        settings.NullValueHandling <- NullValueHandling.Ignore
        settings.Converters.Add(new IdiomaticDuConverter())
        JsonConvert.SerializeObject(obj, Formatting.Indented, settings)

    let deserialize<'a> str = JsonConvert.DeserializeObject<'a>(str)

    let serializeNotify method obj =
        let notify = createNotify method obj
        serialize notify

    let serializeErrorNotify method obj =
        let notify = createErrorNotify method obj
        serialize notify

    let serializeResponse id obj =
        let response = createResponse id obj
        serialize response

    let parseMoveError error = 
        match error with
        | NotYourTurn -> "Not your turn"
        | InvalidMove -> "Invalid move"
        | InvalidInput msg -> sprintf "Invalid input parameter: %s" msg
        | Other msg -> sprintf "Other error: %s" msg
        | _ -> invalidArg "error" error "unknown error %A"

    let serializeErrorResponse id error =
        match error with
        | InvalidStateErrorResponse msg -> sprintf "InvalidState: %s" msg
        | MatchingErrorResponse -> "MatchingError"
        | MoveErrorResponse error -> sprintf "MoveError: %s" (parseMoveError error)
        | InternalErrorResponse -> "InternalError"
        |> createErrorResponse id
        |> serialize

let serializeNotify notify =
    match notify with
    | TestNotify n -> serializeNotify "test" n
    | ChatNotify n -> serializeNotify "chat" n
    | MoveNotify n -> serializeNotify "move" n
    | ErrorNotify (method, n) -> serializeErrorNotify method n
    | EndGameNotify n -> serializeNotify "endgame" n
    | SessionStartNotify n -> serializeNotify "session.start" n
    | SessionCloseNotify n -> serializeNotify "session.close" n
    | _ -> invalidArg "notify" notify "unknown notify"

let serializeResponse id response =
    match response with
    | PingResponse r -> serializeResponse id r
    | ErrorResponse r -> serializeErrorResponse id r
    | OkResponse -> serializeResponse id "Success"
    | _ -> invalidArg "response" response "unknown response"

let deserializeRequest (str:string) =
    let command = deserialize<JsonRpcRequest> str

    match command.Method with
    | "ping" -> 
        command.Id, PingCommand (command.Params.ToObject<PingCommand>())
    | "match" ->
        command.Id, MatchCommand
    | "chat" ->
        command.Id, ChatCommand (command.Params.ToObject<ChatCommand>())
    | "move" ->
        command.Id, MoveCommand (command.Params.ToObject<MoveCommand>())
    | _ -> invalidArg "str" str "unknown request method"

