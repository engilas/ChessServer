module ChessServer.Common.Serializer 

open Types.Command
open FSharp.Json

[<AutoOpen>]
module private Internal =
    let serialize obj = Json.serialize obj
    let inline deserialize str = Json.deserialize str

let serializeMoveCommand (arg: MoveCommand) = serialize arg
let deserializeMoveCommand arg : MoveCommand = deserialize arg
let deserializeNotify arg : Notify = deserialize arg
let serializeMatchOptions (arg: MatchOptions) = serialize arg
let deserializeMatchOptions arg : MatchOptions = deserialize arg

let serializeResponse id response =
    //let message = (id, response)
    let message = Response (id, response)
    serialize message

let serializeNotify (arg: Notify) = 
    let message = Notification arg
    serialize message

let deserializeResponse arg : Response = deserialize arg
let serializeClientMessage (request: RequestDto) = serialize request
let deserializeClientMessage str : RequestDto = deserialize str
let deserializeServerMessage str : ServerMessage = deserialize str