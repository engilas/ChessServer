module SessionTests

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
        | ChatNotify n -> n.Message |> should equal msg
        | _ -> failTest "not a chat notify"
        
    let checkEmptyNotify notify =
        if notify() <> notifyStub then failTest "should be empty notify"

    whiteChat "w"
    checkNotify channels.BlackNotify "w"
    checkEmptyNotify channels.WhiteNotify
    
    channels.Reset()
    
    blackChat "b"
    checkNotify channels.WhiteNotify "b"
    checkEmptyNotify channels.BlackNotify

[<Fact>]
let ``close session check notification`` () =
    let checkInternal session notify1 notify2 msg = 
        let closeNotifyMsg notify = 
            match notify with
            | SessionCloseNotify {Message = msg} -> msg
            | _ -> failTest "not a session close notify"
            
        session.CloseSession msg
        notify1() |> closeNotifyMsg |> should haveSubstring msg
        notify2() |> should equal notifyStub
    
    let channels = channelInfo()

    let sw, _ = channels.CreateSession()
    checkInternal sw channels.BlackNotify channels.WhiteNotify "white"
    
    channels.Reset()
    
    let _, sb = channels.CreateSession()
    checkInternal sb channels.WhiteNotify channels.BlackNotify "black"
    
[<Fact>]
let ``close session check new state`` () =
    // setup
    let channels = channelInfo()
    let call f = f()
    let checkStates() = [channels.WhiteState; channels.BlackState] |> List.iter (call >> should equal New)

    let testAction session = 
        channels.Reset()
        session.CloseSession "test"
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
        (fun () -> s.CloseSession "test") |> should throw sessionError
        (fun () -> s.ChatMessage "test") |> should throw sessionError
        (fun () -> s.CreateMove moveStub) |> should throw sessionError
    
    let sw, sb = channels.CreateSession()
    sw.CloseSession "white"
    checkAnyFunctionThrows sw
    checkAnyFunctionThrows sb
    
    let sw, sb = channels.CreateSession()
    sb.CloseSession "black"
    checkAnyFunctionThrows sw
    checkAnyFunctionThrows sb

[<Theory>]
[<InlineData("a2x")>]
[<InlineData("a")>]
[<InlineData("22")>]
let ``move error - invalid value`` data =
    let checkMoveError source move = async {
        let channels = channelInfo()
        let sw, _ = channels.CreateSession()

        let! result = sw.CreateMove move

        match result with
        | InvalidInput msg -> msg |> should haveSubstring source
        | _ -> failTest "wrong move result"
    }

    async {
        do! checkMoveError "From" {moveStub with From = data; To = "a4"}
        do! checkMoveError "To" {moveStub with From = "a4"; To = data}
    }
