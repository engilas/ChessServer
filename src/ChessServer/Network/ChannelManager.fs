module ChannelManager

open ChessServer.Types.Channel
open System.Collections.Concurrent
open System.Threading
open ChessServer
open System
open Microsoft.Extensions.Logging
open ChessServer.Common.Types.Command

[<AutoOpen>]
module private Internal =
    let private logger = Logging.getLogger("ChannelManager")
    let channels = ConcurrentDictionary<ConnectionId, ClientChannel>()
    let disconnectQuery = ConcurrentDictionary<ConnectionId, Timer>()

    
    let removeDisconnectTimer connectionId = 
        let exists, timer = disconnectQuery.TryRemove connectionId
        if exists then timer.Dispose() else ()

    
            
    //let checkTrue msg x = if not x then failwith msg

    let disconnectTimeout = TimeSpan.FromSeconds(30) //todo TimeSpan.FromSeconds(config.GetValue<double>("DisconnectTimeout"))

    let add x = channels.TryAdd(x.Id, x) |> ignore
    let get x = 
        match channels.TryGetValue x with 
        | (true, v) -> Some v
        | (false, _) -> None
    let remove (x: ConnectionId) = channels.TryRemove x |> ignore
    let getCount () = channels.Count

    let addDisconnectTimeout f connectionId = 
        let timer = new Timer(fun state ->
            try
                f()
                remove connectionId
                logger.LogInformation("Removed channel {ch} due timeout", connectionId)
                removeDisconnectTimer connectionId
            with e ->
                logger.LogError(e, "Error in disconnect timer")
        )
        if disconnectQuery.TryAdd(connectionId, timer) then
            timer.Change(disconnectTimeout, Timeout.InfiniteTimeSpan) |> ignore
        else
            failwithf "Can't add disconnect timer for channel %A - already exists" connectionId

    



let channelManager = {
    Add = add
    Get = get
    Remove = remove
    Count = getCount
    AddDisconnectTimeout = addDisconnectTimeout
    RemoveDisconnectTimeout = removeDisconnectTimer
}


//type MapAgentMessage<'Key, 'Value> =
//    | Add of ('Key * 'Value)
//    | Remove of 'Key
//    | GetMap of AsyncReplyChannel<Map<'Key, 'Value>>
//    | Clear

//let mapAgent = MailboxProcessor<MapAgentMessage<string, int>>.Start(fun inbox ->
//    let rec loop map = async {
//        let! msg = inbox.Receive()
//        match msg with
//        | Add (key, value) -> return! loop (Map.add key value map)
//        | Remove key -> return! loop (Map.remove key map)
//        | GetMap reply -> reply.Reply(map); return! loop map
//        | Clear -> return! loop (Map.empty)
//    }
//    loop Map.empty
//)


//let agent = MailboxProcessor<ClientChannel>.Start(fun inbox ->
//    let rec loop lst = async {
//        let! msg = inbox.Receive()
//        let newLst = 
//            match msg with
//            | GetHistory channel -> channel.Reply lst; lst
//            | PushState (newState, channel) -> channel.Reply(); newState::lst
//            | PopState channel ->
//                match lst with
//                | _ :: xs ->
//                    channel.Reply None; xs
//                | [] -> channel.Reply None; []
//            | Clear channel -> channel.Reply(); []
//        return! loop newLst
//    }
//    loop []
//)