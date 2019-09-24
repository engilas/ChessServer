module SessionStuffTests

open Types.Channel
open FsUnit.Xunit
open SessionTypes
open Types.Command
open Xunit
open TestHelper
open SessionBase

[<Fact>]
let ``chat check notification`` () =
    let channels = channelInfo()

    let ({ChatMessage = whiteChat}, {ChatMessage = blackChat}) = channels.CreateSession()

    let checkNotify notify msg = 
        match notify() with
        | ChatNotify n :: _ -> n.Message |> should equal msg
        | _ -> failTest "not a chat notify"
        
    let checkEmptyNotify notify =
        match notify() with
        | [] -> ()
        | _ -> failTest "should be empty notify"

    whiteChat "w"
    checkNotify channels.Black.GetNotify "w"
    checkEmptyNotify channels.White.GetNotify
    
    channels.Reset()
    
    blackChat "b"
    checkNotify channels.White.GetNotify "b"
    checkEmptyNotify channels.Black.GetNotify

[<Fact>]
let ``close session check notification`` () =
    let checkInternal session notify1 notify2 = 
        let closeNotifyMsg notify = 
            match notify with
            | SessionCloseNotify OpponentDisconnected :: _ -> ()
            | _ -> failTest "not a session close notify"
            
        session.CloseSession OpponentDisconnected
        notify1() |> closeNotifyMsg
        notify2() |> should be Empty
    
    let channels = channelInfo()

    let sw, _ = channels.CreateSession()
    checkInternal sw channels.Black.GetNotify channels.White.GetNotify
    
    channels.Reset()
    
    let _, sb = channels.CreateSession()
    checkInternal sb channels.White.GetNotify channels.Black.GetNotify
    
[<Fact>]
let ``close session check new state`` () =
    // setup
    let channels = channelInfo()
    let call f = f()
    let checkStates() = [channels.White.Channel.GetState; channels.Black.Channel.GetState] |> List.iter (call >> should equal New)

    let testAction session = 
        channels.Reset()
        session.CloseSession OpponentDisconnected
        checkStates()

    // assert
    // white
    let sw, _ = channels.CreateSession()
    testAction sw

    //black
    let _, sb = channels.CreateSession()
    testAction sb

[<Fact>]
let ``close session check exception on any function calls`` () =
    let sessionError = typeof<SessionException>
    let channels = channelInfo()
    let checkAnyFunctionThrows s =
        (fun () -> s.CloseSession OpponentDisconnected) |> should throw sessionError
        (fun () -> s.ChatMessage "test") |> should throw sessionError
        (fun () -> s.CreateMove moveStub) |> should throw sessionError
    
    let sw, sb = channels.CreateSession()
    sw.CloseSession OpponentDisconnected
    checkAnyFunctionThrows sw
    checkAnyFunctionThrows sb
    
    let sw, sb = channels.CreateSession()
    sb.CloseSession OpponentDisconnected
    checkAnyFunctionThrows sw
    checkAnyFunctionThrows sb


