module SessionBase

open System
open System.Threading
open System.Threading.Tasks
open Session
open Types.Channel
open Types.Command
open ChessHelper
open StateContainer

let applyMany x = List.map (fun f -> f x) >> ignore

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
    Reset: unit -> unit
    WaitStateChanged: int -> Async<unit>
    WaitNotify: int -> Async<unit>
}

type TestChannels = {
    White: TestChannel
    Black: TestChannel
    CreateSession: unit -> Session * Session
    Reset: unit -> unit
}

let channelInfo () =
    let createTestChannel id =
        let notifyContainer = createStateHistoryContainer()
        let stateContainer = createStateContainer New
        
        let stateEvent = new SemaphoreSlim(0)
        let notifyEvent = new SemaphoreSlim(0)

        let waitState (sem: SemaphoreSlim) matcher

        let waitNotify container (sem: SemaphoreSlim) matcher =
            let rec wait() = async {
                do! sem.WaitAsync() |> Async.AwaitTask
                match container.GetHistory() with
                | x :: _ when matcher x -> ()
                | _ -> return! wait()
            }
            wait()
        
        let channel = {
            Id = id
            PushNotification = fun n ->
                notifyContainer.PushState n
                notifyEvent.Release() |> ignore
            ChangeState = fun s -> 
                stateContainer.SetState s
                stateEvent.Release() |> ignore
            GetState = stateContainer.GetState
        }

        let reset() =
            notifyContainer.Clear()
            stateContainer.SetState New

        {
            Id = id
            Channel = channel
            GetNotify = notifyContainer.GetHistory
            Reset = reset
            WaitStateChanged = fun () -> wait stateEvent
            WaitNotify = fun () -> wait notifyEvent
        }
    
    let guid = Guid.NewGuid().ToString()

    let white = createTestChannel (sprintf "w%s" guid)
    let black = createTestChannel (sprintf "b%s" guid)
    
    {
        White = white
        Black = black
        Reset = white.Reset >> black.Reset
        CreateSession = fun () -> createSession white.Channel black.Channel
    }

let getMove src dst = {moveStub with Src = positionFromString src; Dst = positionFromString dst}