module SerializeTests

//open Serializer
//open Xunit
//open Types.Command
//open TestHelper
//open FsUnit.Xunit

//[<Fact>]
//let ``serializeNotify correctness`` () =
//    let testMsg = "abcdef123"
//    let notify = {Message=testMsg} |> TestNotify
//    let result = serializeNotify notify

//    sprintf """{
//  "method": "test",
//  "params": {
//    "Message": "%s"
//  }
//}""" testMsg |> should equal result

//[<Fact>]
//let ``serializeResponse correctness`` () =
//    let testMsg = "abcdef123"
//    let id = "151"
//    let response = {Message=testMsg} |> PingResponse
//    let result = serializeResponse id response

//    sprintf """{
//  "result": {
//    "Message": "%s"
//  },
//  "id": "%s"
//}""" testMsg id |> should equal result

//[<Fact>]
//let ``deserializeRequest correctness`` () =
//    let testMsg = "abcdef123"
//    let id = 151
//    let request = 
//        sprintf """{
//    "method": "ping",
//    "id": %d,
//    "params": {
//        "message": "%s"
//    }
//}"""        id testMsg

//    let result = deserializeRequest request
//    (id, PingCommand {Message = testMsg}) |> should equal result

//[<Fact>]
//let ``deserializeRequest errors`` () =
//    let testMsg = "abcdef123"
//    let id = 151
//    let request = 
//        sprintf """{
//    "method": "blabla",
//    "id": %d,
//    "params": {
//        "message": "%s"
//    }
//}"""        id testMsg

//    (fun () -> deserializeRequest request) |> should (throwWithMessage "request") invalidArgument

//[<Fact>]
//let ``serializeResponse ErrorResponse correctness`` () =
//    let msg = "Invalid move"
//    let errorResponse = MoveErrorResponse InvalidMove |> ErrorResponse
//    let id = 151
//    let result = serializeResponse id errorResponse

//    sprintf """{
//  "error": "MoveError: %s",
//  "id": %d
//}""" msg id |> should equal result