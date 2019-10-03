module Serializer 

open Types.Command
open Microsoft.FSharpLu.Json

[<AutoOpen>]
module private Internal =
    let serialize obj = Compact.serialize obj
    let inline deserialize str = Compact.deserialize str

//let serializeRequest (request: ClientMessage) = serialize request

let serializeNotify (notify: Notify) = serialize notify
let serializeResponse (response: Response) = serialize response

