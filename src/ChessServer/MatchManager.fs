module MatchManager
open Helper
open Types.Channel

type RemoveResult = Removed
type AddResult = Queued | OpponentFound 
    
type AddError =
    | AlreadyQueued

type RemoveError =
    | ChannelNotFound

type MatcherResult =
    | AddResult of Result<AddResult, AddError>
    | RemoveResult of Result<RemoveResult, RemoveError>

type Matcher = {
    StartMatch: ClientChannel -> Result<AddResult, AddError>
    StopMatch: ClientChannel -> Result<RemoveResult, RemoveError>
}

[<AutoOpen>]
module private Internal =
    open Types.Command
    open Types.Domain
    open Microsoft.Extensions.Logging
    open Session

    let logger = Logging.getLogger "MatchManager"
    
    type MatcherCommand =
    | Add of ClientChannel
    | Remove of ClientChannel
    
    type MatcherMessage = MatcherCommand * AsyncReplyChannel<MatcherResult>

    let createAgent() = MailboxProcessor<MatcherMessage>.Start(fun inbox -> 
        let rec matcherLoop channels = async {
            let! newList = async {
                try
                    let! command, reply = inbox.Receive()
                
                    let tryMatch = function
                    | black::white::lst ->
                        logger.LogInformation("Matched channels: {1}, {2}", white.Id, black.Id)
                        let whiteSession, blackSession = createSession white black
                        white.ChangeState <| Matched whiteSession
                        black.ChangeState <| Matched  blackSession 
                        SessionStartNotify {Color = White} |> white.PushNotification
                        SessionStartNotify {Color = Black} |> black.PushNotification
                        reply.Reply <| AddResult (Ok OpponentFound)
                        lst
                    | lst ->
                        reply.Reply <| AddResult (Ok Queued)
                        lst

                    return match command with
                    | Add channel ->
                        if channels |> List.exists(fun x -> x.Id = channel.Id) 
                        then reply.Reply <| AddResult (Error AlreadyQueued); channels
                        else
                            channel.ChangeState Matching
                            channel :: channels |> tryMatch
                    | Remove channel ->
                        match channels |> tryRemove (fun x -> x.Id = channel.Id) with
                        | Some lst ->
                            reply.Reply <| RemoveResult (Ok Removed)
                            lst
                        | None ->
                            reply.Reply <| RemoveResult (Error ChannelNotFound)
                            channels
                with e ->
                    logger.LogError(e, "Error in matcher loop")
                    return channels
            }

            return! matcherLoop newList
        }
        matcherLoop []
    )

let createMatcher() =
    let agent = createAgent()
    {
        StartMatch = 
            fun channel ->
                let result = agent.PostAndReply(fun x -> Add channel, x)
                match result with
                | AddResult x -> x
                | _ -> failwith "invalid agent response"
        StopMatch = 
            fun channel -> 
                let result = agent.PostAndReply(fun x -> Remove channel, x)
                match result with
                | RemoveResult x -> x
                | _ -> failwith "invalid agent response"
    }