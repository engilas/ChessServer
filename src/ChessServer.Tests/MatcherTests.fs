module MatcherTests

open FsUnit
open SessionBase
open MatchManager
open FsUnit.Xunit
open TestHelper
open Types.Channel
open Types.Command
open Types.Domain
open Xunit
open FSharp.Collections.ParallelSeq

let checkOkResult res x =
    match x with
    | Ok q when q = res -> ()
    | _ -> failTest "invalid result"

let startMatchCheck matcher channel result = 
    matcher.StartMatch channel |> (checkOkResult result)

let stopMatchCheck matcher channel result = 
    matcher.StopMatch channel |> (checkOkResult result)

let checkMatched channels =
    let checkMatched state notify color =
        match state with
        | Matched _ -> ()
        | _ -> failTest "Invalid state"
        match notify with
        | SessionStartNotify {Color=x} :: [] when x = color -> ()
        | _ -> failTest "Invalid notification"

    checkMatched <| channels.White.Channel.GetState() <| (channels.White.GetNotify()) <| White
    checkMatched <| channels.Black.Channel.GetState() <| (channels.Black.GetNotify()) <| Black

[<Fact>]
let ``startMatch - test state changed, get notify``() = 
    let matcher = createMatcher()
    let channels = channelInfo()
    startMatchCheck matcher channels.White.Channel Queued
    channels.White.Channel.GetState() |> should equal Matching
    channels.White.GetNotify() |> should be Empty
    startMatchCheck matcher channels.Black.Channel OpponentFound

    checkMatched channels

[<Fact>]
let ``startMatch - test duplicates``() =
    let matcher = createMatcher()
    let channels = channelInfo()
    startMatchCheck matcher channels.White.Channel Queued
    match matcher.StartMatch channels.White.Channel with
    | Error AlreadyQueued -> ()
    | _ -> failTest "Invalid match result"
    channels.White.GetNotify() |> should be Empty
    channels.White.Channel.GetState() |> should equal Matching

[<Fact>]
let ``stopMatch test``() =
    let matcher = createMatcher()
    let channels = channelInfo()
    startMatchCheck matcher channels.White.Channel Queued
    stopMatchCheck matcher channels.White.Channel Removed
    match matcher.StopMatch channels.White.Channel with
    | Error ChannelNotFound -> ()
    | _ -> failTest "invalid result"
    startMatchCheck matcher channels.White.Channel Queued
    startMatchCheck matcher channels.Black.Channel OpponentFound
    match matcher.StopMatch channels.White.Channel with
    | Error ChannelNotFound -> ()
    | _ -> failTest "invalid result"

[<Fact>]
let ``stress test``() =
    let matcher = createMatcher()
    let check result = 
        match result with
        | Ok Queued
        | Ok OpponentFound -> ()
        | _ -> failTest "Invalid match result"

    Array.replicate 1000 0
    |> PSeq.iter (fun _ -> 
        let channels = channelInfo()
        matcher.StartMatch channels.White.Channel |> check
        matcher.StartMatch channels.Black.Channel |> check
    )

    // all should be matched
    let channels = channelInfo()
    startMatchCheck matcher channels.White.Channel Queued