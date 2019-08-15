module SessionTests

open ChessServer.Session
open ChessServer.ChannelTypes
open Xunit
open ChessServer.CommandTypes
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

    let (whiteSession, blackSession) = createSession whiteChannel blackChannel

    whiteSession.ChatMessage "w"
    blackSession.ChatMessage "b"

    let checkNotify msg = function
    | ChatNotify n ->
        testEqual n.Message msg


    checkNotify "b" whiteNotify
    checkNotify "w" blackNotify

    ()

