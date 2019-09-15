module MatcherTests

open System
open System.Threading
open FsUnit
open SessionBase
open MatchManager
open FsUnit.Xunit
open TestHelper
open Types.Channel
open Types.Command
open Xunit

let checkOkResult x y = y |> equals (Ok x) 

[<Fact>]
let ``startMatch - test state changed, get notify``() = 
    let channels = channelInfo()
    startMatch channels.White.Channel |> (checkOkResult <| AddResult Queued)
    channels.White.GetState() |> should equal New
    channels.White.GetNotify() |> should be Empty
    startMatch channels.Black.Channel |> (checkOkResult <| AddResult OpponentFound)
    
    let checkMatched state notify =
        match state with
        | Matched _ -> ()
        | _ -> failTest "Invalid state"
        match notify with
        | SessionStartNotify _ :: [] -> ()
        | _ -> failTest "Invalid notification"
    
    checkMatched <| channels.White.GetState() <| (channels.White.GetNotify())
    checkMatched <| channels.Black.GetState() <| (channels.Black.GetNotify())

[<Fact>]
let ``startMatch - test duplicates``() =
    let channels = channelInfo()
    startMatch channels.White.Channel |> (checkOkResult <| AddResult Queued)
    match startMatch channels.White.Channel with
    | Error AlreadyQueued -> ()
    | _ -> failTest "Invalid match result"
    channels.White.GetNotify() |> should be Empty
    channels.White.GetState() |> should equal New

[<Fact>]
let ``stopMatch test``() =
    let channels = channelInfo()
    startMatch channels.White.Channel |> (checkOkResult <| AddResult Queued)
    stopMatch channels.White.Channel |> (checkOkResult <| RemoveResult Removed)
    stopMatch channels.White.Channel |> (checkOkResult <| RemoveResult ChannelNotFound)
    startMatch channels.White.Channel |> (checkOkResult <| AddResult Queued)
    startMatch channels.Black.Channel |> (checkOkResult <| AddResult OpponentFound)
    stopMatch channels.White.Channel |> (checkOkResult <| RemoveResult ChannelNotFound)