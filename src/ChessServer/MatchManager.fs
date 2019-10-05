module MatchManager
open Helper
open Types.Channel
open Types.Command

type RemoveResult = Removed
type AddResult = Queued
    
type AddError =
    | AlreadyQueued

type RemoveError =
    | ChannelNotFound

type MatcherResult =
    | AddResult of Result<AddResult, AddError>
    | RemoveResult of Result<RemoveResult, RemoveError>

type Matcher = {
    StartMatch: ClientChannel -> MatchOptions -> Async<Result<AddResult, AddError>>
    StopMatch: ClientChannel -> Async<Result<RemoveResult, RemoveError>>
}

[<AutoOpen>]
module private Internal =
    open Types.Command
    open Types.Domain
    open Microsoft.Extensions.Logging
    open Session

    let logger = Logging.getLogger "MatchManager"
    
    type MatcherCommand =
    | Add of string * ClientChannel
    | Remove of ClientChannel

    type MatcherMessage = MatcherCommand * AsyncReplyChannel<MatcherResult>

    let createAgent() = MailboxProcessor<MatcherMessage>.Start(fun inbox -> 
        let rec matcherLoop (channels: (string * ClientChannel) list) = async {
            let! command, reply = inbox.Receive()
            let newList = 
                try
                    let rec tryMatch3 = function
                    | black::white::lst ->
                        logger.LogInformation("Matched channels: {1}, {2}", white.Id, black.Id)
                        let whiteSession, blackSession = createSession white black
                        white.ChangeState <| Matched whiteSession
                        black.ChangeState <| Matched blackSession 
                        SessionStartNotify {Color = White} |> white.PushNotification
                        SessionStartNotify {Color = Black} |> black.PushNotification
                        tryMatch3 lst
                    | x -> x
                
                    let tryMatch2 (channels: (string * ClientChannel) list) =
                        channels
                        |> List.groupBy fst
                        |> List.map (fun (key, channels) ->
                             key, tryMatch3 (channels |> List.map snd)
                        )
                        |> List.collect (fun (key, lst) -> lst |> List.map (fun x -> key, x))

                    let find channel = (fun (_, x) -> x.Id = channel.Id)
                    
                    match command with
                    | Add (key, channel) ->
                        if channels |> List.exists (find channel)  
                        then reply.Reply <| AddResult (Error AlreadyQueued); channels
                        else channel.ChangeState Matching
                             let newList = (key, channel) :: channels |> tryMatch2
                             reply.Reply <| AddResult (Ok Queued)
                             newList
                    | Remove channel ->
                        match channels |> tryRemove (find channel) with
                        | Some lst ->
                            reply.Reply <| RemoveResult (Ok Removed)
                            lst
                        | None ->
                            reply.Reply <| RemoveResult (Error ChannelNotFound)
                            channels
                with e ->
                    logger.LogError(e, "Error in matcher loop")
                    channels
            

            return! matcherLoop newList
        }
        matcherLoop []
    )

let createMatcher() =
    let agent = createAgent()
    {
        StartMatch = 
            fun channel options -> async {
                let groupName = options.Group |> Option.defaultValue "general"
                let! result = agent.PostAndAsyncReply(fun x -> Add (groupName, channel), x)
                match result with
                | AddResult x -> return x
                | _ -> return failwith "invalid agent response"
            }
        StopMatch = 
            fun channel -> async {
                let! result = agent.PostAndAsyncReply(fun x -> Remove channel, x)
                match result with
                | RemoveResult x -> return x
                | _ -> return failwith "invalid agent response"
            }
    }