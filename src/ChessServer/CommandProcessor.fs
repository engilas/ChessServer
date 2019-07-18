namespace ChessServer

module CommandProcessor =
    open Types
    open System
    open System.Collections.Concurrent

    type ClientChannel = (ServerMessage -> unit)

    module MatchManager = 
        module private Internal =
            let matchedChannels = new ConcurrentDictionary<string, ClientChannel>()
            let queuedChannels = new ConcurrentQueue<ClientChannel>()

            let rec matcherLoop() = async {
                let rec innerLoop() = 
                    if queuedChannels.Count >= 2 then
                        let _, first = queuedChannels.TryDequeue()
                        let _, second = queuedChannels.TryDequeue()

                        let firstGuid = Guid.NewGuid().ToString()
                        let secondGuid = Guid.NewGuid().ToString()

                        matchedChannels.TryAdd(firstGuid, second) |> ignore
                        matchedChannels.TryAdd(secondGuid, first) |> ignore

                        let notify channel guid = channel (MatchNotify {Channel=guid} |> Notify)
                        notify first firstGuid
                        notify second secondGuid
                        innerLoop()

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
            
        
    open MatchManager

    let processCommand cmd pushResponse = async {
        try
            match cmd with
            | PingCommand ping -> 
                let pong = PingResponse {Message=ping.Message} |> Response
                pushResponse pong
            | MatchCommand ->
                startMatch pushResponse
                let response = MatchResponse {Message="match started"} |> Response
                pushResponse response
            | ChatCommand chat ->
                match getChannel chat.Channel with
                | Some channel -> channel (ChatNotify {Message=chat.Message} |> Notify)
                | None -> pushResponse (ErrorResponse {Message="Channel not found"} |> Response)
        with e ->
            printfn "%s" e.Message
    }

