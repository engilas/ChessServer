module SessionBase

open System.Threading
open System.Threading.Tasks
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
    GetNotify: unit -> Notify list
    GetState: unit -> ClientState
    Reset: unit -> unit
    WaitStateChanged: unit -> Async<unit>
    WaitNotify: unit -> Async<unit>
}

type TestChannels = {
    White: TestChannel
    Black: TestChannel
    CreateSession: unit -> Session * Session
    Reset: unit -> unit
}

let channelInfo () =
    let createTestChannel id =
        let notifyAgent = createStateHistoryAgent()
        let stateAgent = createStateAgent New
        
        let stateEvent = new SemaphoreSlim(0)
        let notifyEvent = new SemaphoreSlim(0)
        
        let waitNewState() =
            stateEvent.WaitAsync() |> Async.AwaitTask
            
        let waitNewNotify() =
            notifyEvent.WaitAsync() |> Async.AwaitTask
        
        let channel = {
            Id = id
            PushNotification = fun n ->
                notifyAgent.Post <| PushState n
                notifyEvent.Release() |> ignore
            ChangeState = fun s -> 
                stateAgent.Post <| SetState s
                stateEvent.Release() |> ignore
        }
        let checkNotify() = notifyAgent.PostAndReply GetHistory
        let checkState() = 
            let x = stateAgent.PostAndReply GetState
            x

        let reset() =
            notifyAgent.Post <| Clear
            stateAgent.Post <| SetState New 

        //channel, checkNotify, checkState, reset, waitNewState, waitNewNotify
        {
            Id = id
            Channel = channel
            GetNotify = checkNotify
            GetState = checkState
            Reset = reset
            WaitStateChanged = waitNewState
            WaitNotify = waitNewNotify
        }
        
    let white = createTestChannel "w"
    let black = createTestChannel "b"
    
    {
        White = white
        Black = black
        Reset = white.Reset >> black.Reset
        CreateSession = fun () -> createSession white.Channel black.Channel
    }

let getMove src dst = {moveStub with Src = positionFromString src; Dst = positionFromString dst}