module MatchManager
open Helper

type RemoveResult = Removed | ChannelNotFound
type AddResult = Queued | OpponentFound 

type MatcherStatus =
    | RemoveResult of RemoveResult
    | AddResult of AddResult
    
type MatcherError =
    | AlreadyQueued

type MatcherResult = Result<MatcherStatus, MatcherError>

[<AutoOpen>]
module private Internal =
    open Types.Channel
    open Types.Command
    open Types.Domain
    open Microsoft.Extensions.Logging
    open Session

    let logger = Logging.getLogger "MatchManager"
    
    type MatcherCommand =
    | Add of ClientChannel
    | Remove of ClientChannel
    
    type MatcherMessage = MatcherCommand * AsyncReplyChannel<MatcherResult>

    let agent = MailboxProcessor<MatcherMessage>.Start(fun inbox -> 
        let rec matcherLoop channels = async {
            try
                let! command, reply = inbox.Receive()
                
                let tryMatch = function
                | first::second::lst ->
                    logger.LogInformation("Matched channels: {1}, {2}", first.Id, second.Id)
                    let whiteSession, blackSession = createSession first second
                    first.ChangeState <| Matched whiteSession
                    second.ChangeState <| Matched blackSession
                    let notify = SessionStartNotify {FirstMove = White}
                    first.PushNotification notify
                    second.PushNotification notify
                    reply.Reply <| Ok (AddResult OpponentFound)
                    lst
                | lst ->
                    reply.Reply <| Ok (AddResult Queued)
                    lst
                    

                let newList =
                    match command with
                    | Add channel ->
                        if channels |> List.exists(fun x -> x.Id = channel.Id) 
                        then reply.Reply <| Error AlreadyQueued; channels
                        else channel :: channels |> tryMatch
                    | Remove channel ->
                        match channels |> tryRemove (fun x -> x.Id = channel.Id) with
                        | Some lst ->
                            reply.Reply <| Ok (RemoveResult Removed)
                            lst
                        | None ->
                            reply.Reply <| Ok (RemoveResult ChannelNotFound)
                            channels

                return! matcherLoop newList
            with e ->
                logger.LogError(e, "Error in matcher loop")
        }
        matcherLoop []
    )

let startMatch x = agent.PostAndReply(fun channel -> Add x, channel)
let stopMatch x = agent.PostAndReply(fun channel -> Remove x, channel)