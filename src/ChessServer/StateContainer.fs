module ChessServer.StateContainer

open System.Threading.Tasks
open FSharp.Control.Tasks.V2

[<AutoOpen>]
module private Internal =
    type StateAgentMessage<'a> =
    | SetState of 'a * AsyncReplyChannel<unit>
    | UpdateState of ('a -> 'a) * AsyncReplyChannel<unit>
    | GetState of AsyncReplyChannel<'a>

    type StateHistoryAgentMessage<'a> =
    | PushState of 'a * AsyncReplyChannel<unit>
    | PopState of AsyncReplyChannel<'a option>
    | GetHistory of AsyncReplyChannel<'a list>
    | Clear of AsyncReplyChannel<unit>

    let waitState (event: Event<'a>) getLast matcher =
        let sub = event.Publish |> Event.filter matcher |> Async.AwaitEvent |> Async.StartAsTask
        let rec wait() = task {
            let last : 'a option = getLast()
            if last.IsNone || not <| matcher last.Value then
                let delay = Task.Delay 1000
                let! r = Task.WhenAny(sub, delay)
                if r = delay then
                    return! wait()
                else
                    return! sub
            else return last.Value
        }
        wait()

type StateContainer<'a> = {
    GetState: unit -> 'a
    SetState: 'a -> unit
    UpdateState: ('a -> 'a) -> unit
    WaitState: ('a -> bool) -> Task<'a>
}

type StateHistoryContainer<'a> = {
    GetHistory: unit -> 'a list
    PushState: 'a -> unit
    PopState: unit -> 'a option
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
                | UpdateState (modify, channel) -> channel.Reply(); modify state
            return! loop state
        }
        loop state
    )

    let getState() = agent.PostAndReply GetState

    let event = Event<_>()
    {
        GetState = getState
        SetState = fun x ->
            agent.PostAndReply (fun ch -> SetState (x, ch))
            event.Trigger x
        UpdateState = fun f ->
            agent.PostAndReply (fun ch -> UpdateState (f, ch))
            getState() |> f |> event.Trigger
        WaitState = waitState event (getState >> Some)
    }

let createStateHistoryContainer() =
    let agent = MailboxProcessor<StateHistoryAgentMessage<'a>>.Start(fun inbox ->
        let rec loop lst = async {
            let! msg = inbox.Receive()
            let newLst = 
                match msg with
                | GetHistory channel -> channel.Reply lst; lst
                | PushState (newState, channel) -> channel.Reply(); newState::lst
                | PopState channel ->
                    match lst with
                    | _ :: xs ->
                        channel.Reply None; xs
                    | [] -> channel.Reply None; []
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
        PopState = fun () -> agent.PostAndReply PopState
        WaitState = waitState event (getHistory >> List.tryHead)
        Clear = fun () -> agent.PostAndReply Clear
    }