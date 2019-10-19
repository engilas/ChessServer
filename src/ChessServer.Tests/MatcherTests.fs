module ChessServer.Tests.MatcherTests

open ChessServer
open ChessServer.Common
open ChessServer.Tests
open FsUnit
open SessionBase
open MatchManager
open FsUnit.Xunit
open Types.Channel
open Types.Command
open Types.Domain
open Xunit
open FSharp.Collections.ParallelSeq

let checkOkResult res x =
    match x with
    | Ok q when q = res -> ()
    | _ -> failTest "invalid result"

let startMatchCheck matcher channel options result = 
    matcher.StartMatch channel options |> (checkOkResult result)

let stopMatchCheck matcher channel result = 
    matcher.StopMatch channel |> (checkOkResult result)

let checkMatchedChannels white black =
    let checkMatched state notify color =
        match state with
        | Matched _ -> ()
        | _ -> failTest "Invalid state"
        match notify with
        | SessionStartNotify {Color=x} :: [] when x = color -> ()
        | _ -> failTest "Invalid notification"
        
    [white, White; black, Black] |> applyMany (fun (x, color) ->
        checkMatched <| x.Channel.GetState() <| (x.GetNotify()) <| color
    )

let checkMatched channels = checkMatchedChannels channels.White channels.Black

[<Fact>]
let ``startMatch - test state changed, get notify``() = 
    let matcher = createMatcher()
    let channels = channelInfo()
    startMatchCheck matcher channels.White.Channel defaultMatcherOptions Queued
    channels.White.Channel.GetState() |> should equal Matching
    channels.White.GetNotify() |> should be Empty
    startMatchCheck matcher channels.Black.Channel defaultMatcherOptions Queued

    checkMatched channels

[<Fact>]
let ``startMatch - test duplicates``() =
    let matcher = createMatcher()
    let channels = channelInfo()
    startMatchCheck matcher channels.White.Channel defaultMatcherOptions Queued
    match matcher.StartMatch channels.White.Channel defaultMatcherOptions with
    | Error AlreadyQueued -> ()
    | _ -> failTest "Invalid match result"
    channels.White.GetNotify() |> should be Empty
    channels.White.Channel.GetState() |> should equal Matching

[<Fact>]
let ``stopMatch test``() =
    let matcher = createMatcher()
    let channels = channelInfo()
    startMatchCheck matcher channels.White.Channel defaultMatcherOptions Queued
    stopMatchCheck matcher channels.White.Channel Removed
    match matcher.StopMatch channels.White.Channel with
    | Error ChannelNotFound -> ()
    | _ -> failTest "invalid result"
    startMatchCheck matcher channels.White.Channel defaultMatcherOptions Queued
    startMatchCheck matcher channels.Black.Channel defaultMatcherOptions Queued
    match matcher.StopMatch channels.White.Channel with
    | Error ChannelNotFound -> ()
    | _ -> failTest "invalid result"

[<Fact>]
let ``stress test``() =
    let matcher = createMatcher()
    let check result = 
        match result with
        | Ok Queued -> ()
        | _ -> failTest "Invalid match result"

    Array.replicate 1000 0
    |> PSeq.iter (fun _ ->
        let channels = channelInfo()
        matcher.StartMatch channels.White.Channel defaultMatcherOptions |> check
        matcher.StartMatch channels.Black.Channel defaultMatcherOptions |> check
    )

    // all should be matched
    let channels = channelInfo()
    startMatchCheck matcher channels.White.Channel defaultMatcherOptions Queued
    
[<Fact>]
let ``test match groups``() =
    let matcher = createMatcher()
    
    let channels1 = channelInfo()
    let channels2 = channelInfo()
    
    let options1 = {Group = Some "1"}
    let options2 = {Group = Some "2"}
    
    startMatchCheck matcher channels1.White.Channel options1 Queued
    startMatchCheck matcher channels1.Black.Channel options2 Queued
    
    [channels1.White; channels1.Black] |> applyMany (fun x ->
        x.Channel.GetState() |> should equal Matching
        x.GetNotify() |> should be Empty
    )
    
    startMatchCheck matcher channels2.Black.Channel options1 Queued
    checkMatchedChannels channels1.White channels2.Black
    startMatchCheck matcher channels2.White.Channel options2 Queued
    checkMatchedChannels channels1.Black channels2.White
    
[<Fact>]
let ``test stop match - new state``() =
    let matcher = createMatcher()
    let channel = channelInfo().White.Channel
    startMatchCheck matcher channel defaultMatcherOptions Queued
    channel.GetState() |> should equal Matching
    stopMatchCheck matcher channel Removed
    channel.GetState() |> should equal New