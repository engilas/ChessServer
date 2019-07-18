namespace ChessServer

module CommandProcessor =
    open CommandTypes
    open ChannelTypes
    open System
    open System.Collections.Concurrent
    open System.Collections.Generic
    open Microsoft.Extensions.Logging
    open Session

    let logger = Logging.getLogger("CommandProcessor")

    module MatchManager = 
        module private Internal =
            let matchedChannels = new ConcurrentDictionary<string, ClientChannel>()
            let queuedChannels = new ConcurrentQueue<ClientChannel>()
            let sessions = new ConcurrentDictionary<string, Color * Session>()

            let rec matcherLoop() = async {
                if queuedChannels.Count >= 2 then
                    let _, first = queuedChannels.TryDequeue()
                    let _, second = queuedChannels.TryDequeue()

                    logger.LogInformation("Matched channels: {1}, {2}", first.Id, second.Id)

                    let firstGuid = Guid.NewGuid().ToString()
                    let secondGuid = Guid.NewGuid().ToString()

                    matchedChannels.TryAdd(firstGuid, second) |> ignore
                    matchedChannels.TryAdd(secondGuid, first) |> ignore

                    let notify channel guid = channel (MatchNotify {Channel=guid} |> Notify)
                    notify first.PushMessage firstGuid
                    notify second.PushMessage secondGuid

                    let session = createSession first second

                    sessions.TryAdd(firstGuid, (White, session)) |> ignore
                    sessions.TryAdd(secondGuid, (Black, session)) |> ignore

                    ()
                else
                    do! Async.Sleep 5000
                return! matcherLoop()
            }

        open Internal

        matcherLoop() |> Async.Start

        let startMatch = queuedChannels.Enqueue
        let getChannel id =
            if matchedChannels.ContainsKey(id) then
                Some matchedChannels.[id]
            else None

        let getSession id =
            if sessions.ContainsKey(id) then
                Some sessions.[id]
            else None
            
            
        
    open MatchManager

    let processCommand cmd channel = async {
        let pushError msg = channel.PushMessage (ErrorResponse {Message=msg} |> Response)

        try
            match cmd with
            | PingCommand ping -> 
                let pong = PingResponse {Message=ping.Message} |> Response
                channel.PushMessage pong
            | MatchCommand ->
                startMatch channel
                let response = MatchResponse {Message="match started"} |> Response
                channel.PushMessage response
            | ChatCommand chat ->
                match getChannel chat.Channel with
                | Some otherChannel -> otherChannel.PushMessage (ChatNotify {Message=chat.Message} |> Notify)
                | None -> pushError "Channel not found"
            | MoveCommand move ->
                match getSession move.Channel with
                | Some (color, session) -> 
                    let! result = session.CreateMove {From = move.From; To = move.To; Source = color}
                    match result with
                    | Ok -> ()
                    | Error -> pushError "Move error"
                | None -> pushError "Session not found"
            | x ->
                failwithf "no processor for the command %A" x
        with e ->
            logger.LogError(e, "Error occurred while processing command")
            let errResponse = (ErrorResponse {Message = sprintf "Internal error: %s" e.Message}) |> Response
            channel.PushMessage errResponse
    }

