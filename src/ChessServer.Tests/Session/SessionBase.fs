﻿module SessionBase

open Session
open Types.Channel
open Types.Command
open ChessHelper

let applyMany x = List.map (fun f -> f x) >> ignore

type StateAgentMessage<'a> =
    | SetState of 'a
    | GetState of AsyncReplyChannel<'a>

type StateHistoryAgentMessage<'a> =
    | PushState of 'a
    | GetHistory of AsyncReplyChannel<'a list>
    | Clear

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

let createStateHistoryAgent() = MailboxProcessor<StateHistoryAgentMessage<'a>>.Start(fun inbox ->
    let rec loop lst = async {
        let! msg = inbox.Receive()
        let newLst = 
            match msg with
            | GetHistory channel -> channel.Reply lst; lst
            | PushState state -> state::lst
            | Clear -> []
        return! loop newLst
    }
    loop []
)

let notifyStub = TestNotify {Message=""}
let moveStub = {
    Src = 0uy
    Dst = 0uy
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
    WhiteNotify: unit -> Notify list
    BlackNotify: unit -> Notify list
    WhiteState: unit -> ClientState
    BlackState: unit -> ClientState
    Reset: unit -> unit
}

let channelInfo () =
    let createChannel() =
        let notifyAgent = createStateHistoryAgent()
        let stateAgent = createStateAgent New
        let channel = {
            Id = ""
            PushNotification = fun n -> notifyAgent.Post <| PushState n 
            ChangeState = fun s -> 
                stateAgent.Post <| SetState s
        }
        let checkNotify() = notifyAgent.PostAndReply GetHistory
        let checkState() = 
            let x = stateAgent.PostAndReply GetState
            x

        let reset() =
            notifyAgent.Post <| Clear
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

let getMove src dst = {moveStub with Src = positionFromString src; Dst = positionFromString dst}