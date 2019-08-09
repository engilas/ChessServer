﻿namespace ChessServer

module JsonRpc =
    

    module private Internal =
        open System.Runtime.Serialization
        open Newtonsoft.Json
        open System
        open Newtonsoft.Json.Linq

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

        let serializeErrorResponse id obj =
            let response = createErrorResponse id obj
            serialize response

    open Internal
    open CommandTypes

    let serializeNotify notify =
        match notify with
        | TestNotify n -> serializeNotify "test" n
        | MatchNotify n -> serializeNotify "match" n
        | ChatNotify n -> serializeNotify "chat" n
        | MoveNotify n -> serializeNotify "move" n
        | ErrorNotify (method, n) -> serializeErrorNotify method n
        | EndGameNotify n -> serializeNotify "endgame" n
        | SessionCloseNotify n -> serializeNotify "session.close" n
        | x -> failwithf "unknown notify %A" x

    let serializeResponse id response =
        match response with
        | PingResponse r -> serializeResponse id r
        | MatchResponse r -> serializeResponse id r
        | ErrorResponse r -> serializeErrorResponse id r
        | x -> failwithf "unknown command %A" x

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
        | x -> failwithf "unknown method %s" x

