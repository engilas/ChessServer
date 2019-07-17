namespace ChessServer

module CommandProcessor =
    open Types
    open System
    open System.Collections.Concurrent

    type ResponseChannel = (OutputCommand -> unit)
    type Message = ResponseChannel * AsyncReplyChannel<ResponseChannel>

    let agent = MailboxProcessor<Message>.Start(fun inbox ->
        let rec loop() = async {
            let! firstResponseChannel, firstReplyChannel = inbox.Receive();
            let! secondResponseChannel, secondReplyChannel = inbox.Receive();

            firstReplyChannel.Reply(secondResponseChannel)
            secondReplyChannel.Reply(firstResponseChannel)
            //do! Async.Sleep 10000
            //replyChannel.Reply(sprintf "%s Smth" message)
            return! loop()
        }
        loop ()
    )

    module MatchManager = 
        module private Internal =
            let matchedChannels = new ConcurrentDictionary<string, ResponseChannel>()

        open Internal

        let registerChannel id channel = matchedChannels.TryAdd(id, channel)
        let getChannel id =
            if matchedChannels.ContainsKey(id) then
                Some matchedChannels.[id]
            else None
            
        
    open MatchManager
    

    let processCommand cmd pushResponse pushNotify = async {
        match cmd with
        | PingCommand ping -> 
            //let! reply = //agent.PostAndAsyncReply(fun replyChannel -> ping.Message, replyChannel)
            let pong = PongCommand {Message=ping.Message}
            pushResponse pong
        | MatchCommand ->
            pushResponse (PongCommand {Message="match started"})
            let! sndResponseChannel = agent.PostAndAsyncReply(fun replyChannel -> pushResponse, replyChannel)
            let matchId = Guid.NewGuid().ToString()
            registerChannel matchId sndResponseChannel |> ignore
            pushResponse (PongCommand {Message=sprintf "matched with id %s" matchId})
        | ChatCommand chat ->
            match getChannel chat.Channel with
            | Some channel -> channel (PongCommand {Message=chat.Message})
            | None -> pushResponse (PongCommand {Message="error"})
    }

