module SerializeTests

open ChessServer.JsonRpc
open Xunit
open ChessServer.CommandTypes
open TestHelper

[<Fact>]
let ``serializeNotify correctness`` () =
    let testMsg = "abcdef123"
    let notify = {Message=testMsg} |> TestNotify
    let result = serializeNotify notify

    testEqual (sprintf """{
  "method": "test",
  "params": {
    "Message": "%s"
  }
}""" testMsg) result

[<Fact>]
let ``serializeResponse correctness`` () =
    let testMsg = "abcdef123"
    let id = 151
    let response = {Message=testMsg} |> PingResponse
    let result = serializeResponse id response

    testEqual (sprintf """{
  "result": {
    "Message": "%s"
  },
  "id": %d
}""" testMsg id) result

[<Fact>]
let ``deserializeRequest correctness`` () =
    let testMsg = "abcdef123"
    let id = 151
    let request = 
        sprintf """{
    "method": "ping",
    "id": %d,
    "params": {
        "message": "%s"
    }
}"""        id testMsg

    let result = deserializeRequest request
    let expected = id, PingCommand {Message = testMsg}

    Assert.Equal(expected, result)

[<Fact>]
let ``deserializeRequest errors`` () =
    let testMsg = "abcdef123"
    let id = 151
    let request = 
        sprintf """{
    "method": "blabla",
    "id": %d,
    "params": {
        "message": "%s"
    }
}"""        id testMsg

    throwsInvalidArgWithMessage "request" (fun () -> deserializeRequest request) |> ignore