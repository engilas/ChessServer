module ClientChannelAdapter
open System
open Types.Channel
open Types.Command

//type ClientChannel = {
//    Id: string
//    PushNotification: Notify -> unit
//    ChangeState: ClientState -> unit
//    GetState: unit -> ClientState
//}

let getClientChannel
   (id: string)
   (notify: Action<Notify>)
   (changeState: Action<ClientState>)
   (getState: Func<ClientState>) =
   {
       Id = id
       PushNotification = fun n -> notify.Invoke(n)
       ChangeState = fun s -> changeState.Invoke(s)
       GetState = fun () -> getState.Invoke()
   }