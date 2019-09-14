module MatcherTests

open FsUnit
open SessionBase
open MatchManager
open FsUnit.Xunit
open TestHelper
open Types.Channel
open Types.Command
open Xunit

[<Fact>]
let ``matched - test state changed``() = async {
    let channels = channelInfo()
    startMatch channels.White.Channel
    channels.White.GetState() |> should equal New
    channels.White.GetNotify() |> should be Empty
    startMatch channels.Black.Channel
    
    do! channels.White.WaitNotify()
    
    let checkMatched state notify =
        match state with
        | Matched _ -> ()
        | _ -> failTest "Invalid state"
        match notify with
        | SessionStartNotify _ :: [] -> ()
        | _ -> failTest "Invalid notification"
    
    checkMatched <| channels.White.GetState() <| (channels.White.GetNotify())
    checkMatched <| channels.Black.GetState() <| (channels.Black.GetNotify())
}