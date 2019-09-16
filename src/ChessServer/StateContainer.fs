module StateContainer

open System.Threading

[<AutoOpen>]
module private Internal =
    type StateAgentMessage<'a> =
    | SetState of 'a * AsyncReplyChannel<unit>
    | GetState of AsyncReplyChannel<'a>

    type StateHistoryAgentMessage<'a> =
    | PushState of 'a * AsyncReplyChannel<unit>
    | GetHistory of AsyncReplyChannel<'a list>
    | Clear of AsyncReplyChannel<unit>

type StateContainer<'a> = {
    GetState: unit -> 'a
    SetState: 'a -> unit
    WaitState: ('a -> bool) -> Async<'a>
}

type StateHistoryContainer<'a> = {
    GetHistory: unit -> 'a list
    PushState: 'a -> unit
    WaitState: ('a -> bool) -> Async<'a>
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
    let sem = new SemaphoreSlim(0)

    let waitState matcher =
        let rec wait() = async {
            do! sem.WaitAsync() |> Async.AwaitTask
            match getState() with
            | x when matcher x -> return x
            | _ -> return! wait()
        }
        wait()

    {
        GetState = getState
        SetState = 
            fun x -> 
                agent.PostAndReply (fun ch -> SetState (x, ch))
                sem.Release() |> ignore
        WaitState = waitState
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
    let sem = new SemaphoreSlim(0)

    let waitState matcher =
        let rec wait() = async {
            do! sem.WaitAsync() |> Async.AwaitTask
            match getHistory() with
            | x :: _ when matcher x -> return x
            | _ -> return! wait()
        }
        wait()

    {
        GetHistory = getHistory
        PushState = fun x -> agent.PostAndReply (fun ch -> PushState (x, ch))
        WaitState = waitState
        Clear = fun () -> agent.PostAndReply Clear
    }