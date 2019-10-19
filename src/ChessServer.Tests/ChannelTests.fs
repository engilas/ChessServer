module ChessServer.Tests.ChannelTests

open ChessServer
open ChessServer.Common
open System.Threading.Tasks
open ClientChannelManager
open FSharp.Control.Tasks.V2
open FsUnit.Xunit
open StateContainer
open Xunit
open Types.Command
open Types.Channel

let notifyStub _ = ()

[<Fact>]
let ``test id``() =
    let channel = createChannel "abcd" notifyStub
    channel.Id |> should equal "abcd"
    
[<Fact>]
let ``test state``() =
    let channel = createChannel "abcd" notifyStub
    channel.GetState() |> should equal New
    
[<Fact>]
let ``test is disconnected``() =
    let channel = createChannel "abcd" notifyStub
    channel.IsDisconnected() |> should equal false
    
[<Fact>]
let ``test push notification - connected state``() = task {
    let container = createStateHistoryContainer()
    let channel = createChannel "abcd" container.PushState
    let notify = ChatNotify {Message = "qwe"}
    channel.PushNotification notify
    let! _ = container.WaitState (fun x -> x = ("abcd", notify))
    ()
}
   
   
[<Fact>]
let ``test change state``() =
    let channel = createChannel "abcd" notifyStub
    channel.ChangeState Matching
    channel.GetState() |> should equal Matching
    
[<Fact>]
let ``test disconnect``() =
     let channel = createChannel "abcd" notifyStub
     channel.Disconnect()
     channel.IsDisconnected() |> should equal true
     
[<Fact>]
let ``test reconnect``() =
     let channel = createChannel "abcd" notifyStub
     channel.ChangeState Matching
     channel.Disconnect()
     channel.Reconnect "qwe"
     channel.GetState() |> should equal Matching
     channel.IsDisconnected() |> should equal false
     
[<Fact>]
let ``test push notification - disconnected state``() = task {
    let container = createStateHistoryContainer()
    let channel = createChannel "abcd" container.PushState
    channel.Disconnect()
    channel.PushNotification <| ChatNotify {Message = "qwe"}
    do! Task.Delay(1000)
    container.PopState() |> should equal None
    channel.Reconnect "asd"
    let! _ = container.WaitState (fun x -> x = ("asd", ChatNotify {Message = "qwe"}))
    ()
}
     