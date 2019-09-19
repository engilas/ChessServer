module Serializer 

open Types.Command
open Microsoft.FSharpLu.Json

[<AutoOpen>]
module private Internal =
    let serialize obj = Compact.serialize obj
    let inline deserialize str = Compact.deserialize str

    type ResponseDto = {
        Id: string
        Response: Response
    }

let serializeRequest (request: ClientMessage) = serialize request

let serializeNotify notify = 
    let message = Notification notify
    serialize message

let serializeResponse id response =
    let message = { Id = id; Response = response }
    serialize message

let deserializeClientMessage str : ClientMessage = deserialize str
let deserializeServerMessage str : ServerMessage = deserialize str

