module SessionTests

open Session
open Types.Channel
open FsUnit.Xunit
open SessionTypes
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
let moveStub = {
    From = ""
    To = "" 
    PawnPromotion = None
}

type TestChannel = {
    Id: string
    Channel: ClientChannel
}

type TestChannels = {
    White: TestChannel
    Black: TestChannel
    CreateSession: unit -> Session * Session
    WhiteNotify: unit -> Notify
    BlackNotify: unit -> Notify
    WhiteState: unit -> ClientState
    BlackState: unit -> ClientState
    Reset: unit -> unit
}

let channelInfo () =
    let createChannel() =
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
        
    let wChannel, wCheckNotify, wCheckState, wReset = createChannel()
    let bChannel, bCheckNotify, bCheckState, bReset = createChannel()
    let whiteId = "w"
    let blackId = "b"
    
    {
        White = {
            Id = whiteId
            Channel = wChannel
        }
        Black = {
            Id = blackId
            Channel = bChannel
        }
        WhiteNotify = wCheckNotify
        BlackNotify = bCheckNotify
        WhiteState = wCheckState
        BlackState = bCheckState
        Reset = wReset >> bReset
        CreateSession = fun () -> createSession wChannel bChannel
    }


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
