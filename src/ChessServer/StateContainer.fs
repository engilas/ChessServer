module StateContainer

open System.Threading
open System
open System.Threading.Tasks

[<AutoOpen>]
module private Internal =
    type StateAgentMessage<'a> =
    | SetState of 'a * AsyncReplyChannel<unit>
    | GetState of AsyncReplyChannel<'a>

    type StateHistoryAgentMessage<'a> =
    | PushState of 'a * AsyncReplyChannel<unit>
    | GetHistory of AsyncReplyChannel<'a list>
    | Clear of AsyncReplyChannel<unit>

    let waitState (event: Event<_>) matcher =
        let sub = event.Publish |> Event.filter matcher
        Async.AwaitEvent sub |> Async.StartAsTask

type StateContainer<'a> = {
    GetState: unit -> 'a
    SetState: 'a -> unit
    WaitState: ('a -> bool) -> Task<'a>
}

type StateHistoryContainer<'a> = {
    GetHistory: unit -> 'a list
    PushState: 'a -> unit
    WaitState: ('a -> bool) -> Task<'a>
    Clear: unit -> unit
}

let createStateContainer state = 
    let agent = MailboxProcessor<StateAgentMessage<'a>>.Start(fun inbox ->
        let rec loop state = async {
            let! msg = inbox.Receive()
            let state = 
                match msg with
                | GetState channel -> channel.Reply state; state
                | SetState (newState, channel) -> channel.Reply(); newState
            return! loop state
        }
        loop state
    )

    let getState() = agent.PostAndReply GetState
    let event = Event<_>()

    {
        GetState = getState
        SetState = 
            fun x -> 
                agent.PostAndReply (fun ch -> SetState (x, ch))
                event.Trigger x
        WaitState = waitState event
    }

let createStateHistoryContainer() =
    let agent = MailboxProcessor<StateHistoryAgentMessage<'a>>.Start(fun inbox ->
        let rec loop lst = async {
            let! msg = inbox.Receive()
            let newLst = 
                match msg with
                | GetHistory channel -> channel.Reply lst; lst
                | PushState (newState, channel) -> channel.Reply(); newState::lst
                | Clear channel -> channel.Reply(); []
            return! loop newLst
        }
        loop []
    )

    let getHistory() = agent.PostAndReply GetHistory
    let event = Event<_>()

    {
        GetHistory = getHistory
        PushState = fun x -> 
            agent.PostAndReply (fun ch -> PushState (x, ch))
            event.Trigger x
        WaitState = waitState event
        Clear = fun () -> agent.PostAndReply Clear
    }