module CommandProcessorTests

open FsUnit
open Xunit
open CommandProcessor
open TestHelper
open Types.Channel
open Types.Command
open SessionBase
open MatchManager
open StateContainer
open System.Threading

let getChannel matcher = 
    let channels = channelInfo()
    let query = createCommandProcessor channels.White.Channel matcher
    query, channels.White

let getChannelSimple() = getChannel (createMatcher())

let matcherStub() =
    let failFun _ = failTest "should not be called"
    {
        StartMatch = failFun
        StopMatch = failFun
    }

let checkEmptyNotify getNotify = match getNotify() with [] -> () | _ -> failTest "wrong notify"
let checkOkResult f = async {
    let! result = f
    result |> should equal OkResponse
}

[<Fact>]
let ``test change state``() = async {
    let query, info = getChannel (createMatcher())
    info.Channel.ChangeState Matching
    let! response = query MatchCommand
    match response with
    | ErrorResponse (InvalidStateErrorResponse x) -> x |> should haveSubstring "matched"
    | x -> failTestf "Invalid response %A" x
}

[<Fact>]
let ``test ping command``() = async {
    let query, _ = getChannelSimple()
    let! response = query (PingCommand {Message="test"}) 
    match response with
    | PingResponse {Message="test"} -> ()
    | _ -> failTest "invalid response"
}

[<Fact>]
let ``test match command - matched``() = async {
    let stateContainer = createStateContainer false
    let matcher = { 
        matcherStub() with
            StartMatch = fun _ -> 
                stateContainer.SetState true
                Ok Queued
        }
    let query, info = getChannel matcher
    do! query MatchCommand |> checkOkResult
    checkEmptyNotify info.GetNotify
    stateContainer.GetState() |> should equal true
}

[<Fact>]
let ``test match command - matcher error``() = async {
    let stateContainer = createStateContainer false
    let matcher = { 
        matcherStub() with
            StartMatch = fun _ -> 
                stateContainer.SetState true
                Error AlreadyQueued
        }
    let query, info = getChannel matcher
    let! response = query MatchCommand
    match response with
    | ErrorResponse MatchingErrorResponse -> ()
    | _ -> failTest "invalid response"
    checkEmptyNotify info.GetNotify
    stateContainer.GetState() |> should equal true
}

[<Fact>]
let ``test chat command - invalid state``() = async {
    let query, _ = getChannelSimple()
    let! result = query (ChatCommand {Message=""}) 
    match result with
    | ErrorResponse (InvalidStateErrorResponse msg) -> msg |> should haveSubstring "Not matched"
    | _ -> failTest "invalid result"
}

[<Fact>]
let ``test chat command - correctness``() = async {
    let matcher = createMatcher()
    let query1, _ = getChannel matcher
    let query2, info2 = getChannel matcher

    do! query1 MatchCommand |> checkOkResult
    do! query2 MatchCommand |> checkOkResult

    do! ChatCommand {Message = "test"} |> query1 |> checkOkResult
    do! info2.WaitNotify
    match info2.GetNotify() with
    | ChatNotify {Message=msg} :: _ -> msg |> equals "test"
    | x -> failTest "No chat notify"
}

[<Fact>]
let ``test move command - invalid state``() = async {
    let query, _ = getChannelSimple()
    
    let makeInvalidMove() = async {
        let move = Unchecked.defaultof<MoveCommand>
        let! result = query (MoveCommand move)
        match result with
        | ErrorResponse (InvalidStateErrorResponse msg) -> msg |> should haveSubstring "Not matched"
        | _ -> failTest "invalid result"
    }

    do! makeInvalidMove()
    do! query MatchCommand |> checkOkResult
    do! makeInvalidMove()
}


[<Fact>]
let ``test move command - invalid move``() = async {
    let matcher = createMatcher()
    let query1, _ = getChannel matcher
    let query2, info2 = getChannel matcher

    let makeInvalidMove query error = async {
        let move = {Src = 0uy; Dst = 0uy; PawnPromotion = None}
        let! result = query (MoveCommand move)
        match result with
        | ErrorResponse (MoveErrorResponse e) -> e |> equals error
        | _ -> failTest "invalid result"
    }

    do! query1 MatchCommand |> checkOkResult
    do! query2 MatchCommand |> checkOkResult
    do! makeInvalidMove query1 InvalidMove
    do! makeInvalidMove query2 NotYourTurn
}

//[<Fact>]
//let ``test move command - correctness``() = async {
//    let matcher = createMatcher()
//    let query1, _ = getChannel matcher
//    let query2, _ = getChannel matcher

//    let makeValidMove move = async {
//        let! result = query (MoveCommand move)
//        match result with
//        | ErrorResponse (InvalidStateErrorResponse msg) -> msg |> should haveSubstring "Not matched"
//        | _ -> failTest "invalid result"
//    }
//}