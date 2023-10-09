module ChessServer.Tests.CommandProcessorTests

open ChessServer
open ChessServer.Common
open ChessServer.Tests
open FsUnit
open Xunit
open CommandProcessor
open Types.Channel
open Types.Command
open SessionBase
open MatchManager
open StateContainer
open ChessHelper
open SessionTypes

let getChannel matcher = 
    let channels = channelInfo()
    let query = processCommand matcher ChannelManager.channelManager (channels.White.Channel)
    query, channels.White

let getChannelSimple() = getChannel (createMatcher())

let matcherStub() =
    let failFun _ = failTest "should not be called"
    {
        StartMatch = failFun
        StopMatch = failFun
    }

let checkEmptyNotify getNotify = match getNotify() with [] -> () | _ -> failTest "wrong notify"
let checkOkResult f = 
    let result = f
    result |> should equal OkResponse

let matchConnections con1 con2 =
    MatchCommand defaultMatcherOptions |> con1 |> checkOkResult
    MatchCommand defaultMatcherOptions |> con2 |> checkOkResult


[<Fact>]
let ``test change state``() =
    let query, info = getChannel (createMatcher())
    info.Channel.ChangeState Matching
    let response = MatchCommand defaultMatcherOptions |> query
    match response with
    | ErrorResponse (InvalidStateErrorResponse x) -> x |> should haveSubstring "matched"
    | x -> failTestf "Invalid response %A" x

[<Fact>]
let ``test ping command``() = 
    let query, _ = getChannelSimple()
    let response = query (PingCommand {Message="test"}) 
    match response with
    | PingResponse {Message="test"} -> ()
    | _ -> failTest "invalid response"

[<Fact>]
let ``test match command - matched``() = 
    let stateContainer = createStateContainer false
    let matcher = { 
        matcherStub() with
            StartMatch = fun _ _ -> 
                stateContainer.SetState true
                Ok Queued
        }
    let query, info = getChannel matcher
    MatchCommand defaultMatcherOptions |> query |> checkOkResult
    checkEmptyNotify info.GetNotify
    stateContainer.GetState() |> should equal true

[<Fact>]
let ``test match command - matcher error``() =
    let stateContainer = createStateContainer false
    let matcher = { 
        matcherStub() with
            StartMatch = fun _ _ -> 
                stateContainer.SetState true
                Error AlreadyQueued
        }
    let query, info = getChannel matcher
    let response = MatchCommand defaultMatcherOptions |> query
    match response with
    | ErrorResponse MatchingErrorResponse -> ()
    | _ -> failTest "invalid response"
    checkEmptyNotify info.GetNotify
    stateContainer.GetState() |> should equal true

[<Fact>]
let ``test chat command - invalid state``() = 
    let query, _ = getChannelSimple()
    let result = query (ChatCommand {Message=""}) 
    match result with
    | ErrorResponse (InvalidStateErrorResponse msg) -> msg |> should haveSubstring "Not matched"
    | _ -> failTest "invalid result"

[<Fact>]
let ``test chat command - correctness``() = 
    let matcher = createMatcher()
    let whiteQuery, _ = getChannel matcher
    let blackQuery, blackInfo = getChannel matcher

    matchConnections whiteQuery blackQuery

    ChatCommand {Message = "test"} |> whiteQuery |> checkOkResult
    match blackInfo.GetNotify() with
    | ChatNotify {Message = msg} :: _ -> msg |> equals "test"
    | _ -> failTest "No chat notify"

[<Fact>]
let ``test move command - invalid state``() =
    let query, _ = getChannelSimple()
    
    let makeInvalidMove() = 
        let move = Unchecked.defaultof<MoveCommand>
        let result = query (MoveCommand move)
        match result with
        | ErrorResponse (InvalidStateErrorResponse msg) -> msg |> should haveSubstring "Not matched"
        | _ -> failTest "invalid result"

    makeInvalidMove()
    MatchCommand defaultMatcherOptions |> query |> checkOkResult
    makeInvalidMove()


[<Fact>]
let ``test move command - invalid move``() =
    let matcher = createMatcher()
    let query1, _ = getChannel matcher
    let query2, _ = getChannel matcher

    let makeInvalidMove query error = 
        let move = {Move = {Src = 0uy; Dst = 0uy}; PawnPromotion = None}
        let result = query (MoveCommand move)
        match result with
        | ErrorResponse (MoveErrorResponse e) -> e |> equals error
        | _ -> failTest "invalid result"

    matchConnections query1 query2
    makeInvalidMove query1 InvalidMove
    makeInvalidMove query2 NotYourTurn

[<Fact>]
let ``test move command - correctness``() = 
    let matcher = createMatcher()
    let whiteQuery, whiteInfo = getChannel matcher
    let blackQuery, blackInfo = getChannel matcher

    let makeValidMove query src dst oppentNotify =
        let move = {Move = {Src = positionFromString src; Dst = positionFromString dst}; PawnPromotion = None}
        let result = query (MoveCommand move)
        match result with
        | OkResponse -> ()
        | _ -> failTest "invalid result"
        match oppentNotify() with
        | MoveNotify {Primary = {Src = src; Dst = dst}} :: _ 
            when src = move.Move.Src && dst = move.Move.Dst -> ()
        | x -> failTest "invalid notify"

    matchConnections whiteQuery blackQuery
    makeValidMove whiteQuery "a2" "a4" blackInfo.GetNotify
    makeValidMove blackQuery "a7" "a6" whiteInfo.GetNotify

[<Fact>]
let ``test disconnect command - new state - doing nothing``() =
    let whiteQuery, _ = getChannelSimple()
    whiteQuery DisconnectCommand |> checkOkResult

[<Fact>]
let ``test disconnect command - matching state - stop matching``() =
    let matcher = createMatcher()
    let whiteQuery, whiteInfo = getChannel matcher
    MatchCommand defaultMatcherOptions |> whiteQuery |> checkOkResult
    whiteQuery DisconnectCommand |> checkOkResult
    match matcher.StopMatch whiteInfo.Channel with
    | Error ChannelNotFound -> () // already stopped
    | _ -> failTest "matching should be stopped"

[<Fact>]
let ``test disconnect command - active session - stop session``() =
    let matcher = createMatcher()
    let whiteQuery, whiteInfo = getChannel matcher
    let blackQuery, blackInfo = getChannel matcher
    matchConnections whiteQuery blackQuery

    let session = 
        match blackInfo.Channel.GetState() with
        | Matched session -> session
        | x -> failTest "invalid state"

    whiteQuery DisconnectCommand |> checkOkResult

    let checkState = function
    | New -> ()
    | x -> failTest "invalid state"
    checkState <| whiteInfo.Channel.GetState()
    checkState <| blackInfo.Channel.GetState()

    (fun () -> session.ChatMessage "") |> should throw typeof<SessionException>
    
[<Fact>]
let ``test disconnect command - double disconnect``() =
    let matcher = createMatcher()
    let query, info = getChannel matcher
    (query <| MatchCommand defaultMatcherOptions) |> should equal OkResponse
    info.Channel.GetState() |> should equal Matching
    query DisconnectCommand |> should equal OkResponse
    info.Channel.GetState() |> should equal New
    query DisconnectCommand |> should equal OkResponse
    info.Channel.GetState() |> should equal New