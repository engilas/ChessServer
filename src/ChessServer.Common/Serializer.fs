module ChessServer.Common.Serializer 

open Types.Command
open FSharp.Json

[<AutoOpen>]
module private Internal =
    let serialize obj = Json.serialize obj
    let inline deserialize str = Json.deserialize str

let serializeMoveCommand (arg: MoveCommand) = serialize arg
let deserializeMoveCommand arg : MoveCommand = deserialize arg
let serializeNotify (arg: Notify) = serialize arg
let deserializeNotify arg : Notify = deserialize arg
let serializeMatchOptions (arg: MatchOptions) = serialize arg
let deserializeMatchOptions arg : MatchOptions = deserialize arg

let serializeResponse (arg: Response) = serialize arg
let deserializeResponse arg : Response = deserialize arg