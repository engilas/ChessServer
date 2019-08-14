module SerializeTests

open ChessServer.JsonRpc
open Xunit
open ChessServer.CommandTypes

[<Fact>]
let ``serializeNotify correctness`` () =
    let testMsg = "abcdef123"
    let notify = {Message=testMsg} |> TestNotify
    let result = serializeNotify notify

    Assert.Equal("""{
  "method": "test",
  "params": {
    "Message": "abcdef123"
  }
}""", result)

[<Fact>]
let ``serializeResponse correctness`` () =
    let testMsg = "abcdef123"
    let response = {Message=testMsg} |> PingResponse
    let result = serializeResponse 151 response

    Assert.Equal("""{
  "result": {
    "Message": "abcdef123"
  },
  "id": 151
}""", result)

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
    
    ()