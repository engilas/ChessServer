﻿module SessionBase

open System
open System.Threading
open System.Threading.Tasks
open Session
open Types.Channel
open Types.Command
open ChessHelper
open StateContainer

let applyMany x = List.map (fun f -> f x) >> ignore

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
    //WaitStateChanged: (ClientState -> bool) -> Async<ClientState>
   // WaitNotify: (Notify -> bool) -> Async<Notify>
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
        
        let channel = {
            Id = id
            PushNotification = notifyContainer.PushState
            ChangeState = stateContainer.SetState
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
            //WaitStateChanged = stateContainer.WaitState
            //WaitNotify = notifyContainer.WaitState
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