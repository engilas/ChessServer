module SessionBase

open Session
open Types.Channel
open Types.Command

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

let getMove from _to = {moveStub with From = from; To = _to}