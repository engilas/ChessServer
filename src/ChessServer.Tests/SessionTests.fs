module SessionTests

open ChessServer.Session
open ChessServer.ChannelTypes
open FsUnit.Xunit
open ChessServer.CommandTypes
open Xunit
open TestHelper

let channelStub = {
    Id = ""
    PushNotification = fun _ -> ()
    ChangeState = fun _ -> async.Return ()
}

[<Fact>]
let ``chat check notification`` () =
    let mutable whiteNotify = TestNotify {Message=""}
    let mutable blackNotify = TestNotify {Message=""}

    let whiteChannel = { channelStub with PushNotification = fun notify -> whiteNotify <- notify }
    let blackChannel = { channelStub with PushNotification = fun notify -> blackNotify <- notify }

    let ({ChatMessage = whiteChat}, {ChatMessage = blackChat}) = createSession whiteChannel blackChannel

    whiteChat "w"
    blackChat "b"

    let checkNotify msg = function
    | ChatNotify n -> n.Message |> should equal msg
    | _ -> failTest "not a chat notify"

    checkNotify "b" whiteNotify
    checkNotify "w" blackNotify

[<Fact>]
let ``close session check notification`` () =
    let mutable whiteNotify = TestNotify {Message=""}
    let mutable blackNotify = TestNotify {Message=""}

    let whiteChannel = { channelStub with PushNotification = fun notify -> whiteNotify <- notify }
    let blackChannel = { channelStub with PushNotification = fun notify -> blackNotify <- notify }

    //let ({CloseSession = closeWhite}, {CloseSession = closeBlack}) = createSession whiteChannel blackChannel
    let (s1, s2) = createSession whiteChannel blackChannel
    //todo что делать если закрыть сессию но продолжать ею пользоваться
    s1.CloseSession "qq"
    s1.ChatMessage "gg"
    s1.CreateMove {
        From = "a2"
        To = "a4" 
        PawnPromotion = None
    }
    //closeWhite "w"

    //let checkNotify msg = function
    //| ChatNotify n -> n.Message |> should equal msg
    //| _ -> failTest "not a chat notify"

    //checkNotify "b" whiteNotify
    //checkNotify "w" blackNotify
