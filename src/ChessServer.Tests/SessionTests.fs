module SessionTests

open Session
open Types.Channel
open FsUnit.Xunit
open Types.Command
open Xunit
open TestHelper

let applyMany x = List.map (fun f -> f x) >> ignore

type StateAgentMessage<'a> =
    | SetState of 'a
    | GetState of AsyncReplyChannel<'a>

let createStateAgent state = MailboxProcessor<StateAgentMessage<'a>>.Start(fun inbox ->
    let rec loop state = async {
        let! msg = inbox.Receive()
        let state = 
            match msg with
            | GetState channel -> channel.Reply state; state
            | SetState state -> state
        return! loop state
    }
    loop state
)

let notifyStub = TestNotify {Message=""}

let channelInfo () =
    let notifyAgent = createStateAgent notifyStub
    let stateAgent = createStateAgent New
    let channel = {
        Id = ""
        PushNotification = fun n -> notifyAgent.Post <| SetState n 
        ChangeState = fun s -> 
            stateAgent.Post <| SetState s
    }
    let checkNotify() = notifyAgent.PostAndReply GetState
    let checkState() = 
        let x = stateAgent.PostAndReply GetState
        x

    let reset() =
        notifyAgent.Post <| SetState notifyStub
        stateAgent.Post <| SetState New 

    channel, checkNotify, checkState, reset


[<Fact>]
let ``chat check notification`` () =
    let whiteChannel, whiteNotify, _, _ = channelInfo()
    let blackChannel, blackNotify, _, _ = channelInfo()

    let ({ChatMessage = whiteChat}, {ChatMessage = blackChat}) = createSession whiteChannel blackChannel

    whiteChat "w"
    blackChat "b"

    let checkNotify notify msg = 
        match notify() with
        | ChatNotify n -> n.Message |> should equal msg
        | _ -> failTest "not a chat notify"

    checkNotify whiteNotify "b"
    checkNotify blackNotify "w"

[<Fact>]
let ``close session check notification`` () =
    let checkInternal session notify msg = 
        let notifyMsg notify = 
            match notify with
            | SessionCloseNotify {Message = msg} -> msg
            | _ -> failTest "not a session close notify"
            
        session.CloseSession msg
        notify() |> notifyMsg |> should haveSubstring msg
    
    let whiteChannel, whiteNotify, _, _ = channelInfo()
    let blackChannel, blackNotify, _, _ = channelInfo()

    let sw, sb = createSession whiteChannel blackChannel
    checkInternal sw blackNotify "white"
    checkInternal sb whiteNotify "black"
    
[<Fact>]
let ``close session check new state`` () =
    // setup
    let whiteChannel, _, whiteState, _ = channelInfo()
    let blackChannel, _, blackState, _ = channelInfo()
    let createSession = createSession whiteChannel blackChannel
    let resetStates() = 
        [whiteChannel.ChangeState; blackChannel.ChangeState]
        |> applyMany Matching
    let call f = f()
    let checkStates() = [whiteState; blackState] |> List.iter (call >> should equal New)

    let testAction session = 
        resetStates()
        session.CloseSession "test"
        checkStates()

    // assert
    // white
    let sw, _ = createSession
    testAction sw

    //black
    let _, sb = createSession
    testAction sb


    //s1.ChatMessage "gg"
    //s1.CreateMove {
    //    From = "a2"
    //    To = "a4" 
    //    PawnPromotion = None
    //} |> ignore
    //()
