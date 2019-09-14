module MatchManager

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

    let agent = MailboxProcessor.Start(fun inbox -> 
        let rec matcherLoop channels = async {
            try
                let channels = 
                    match channels with
                    | first::second::lst ->
                        logger.LogInformation("Matched channels: {1}, {2}", first.Id, second.Id)
                        let whiteSession, blackSession = createSession first second
                        first.ChangeState <| Matched whiteSession
                        second.ChangeState <| Matched blackSession
                        let notify = SessionStartNotify {FirstMove = White}
                        first.PushNotification notify
                        second.PushNotification notify
                        lst
                    | lst -> lst

                let! command = inbox.Receive()

                let list =
                    match command with
                    | Add channel -> channel :: channels
                    | Remove channel -> channels |> List.filter (fun x -> x.Id <> channel.Id)

                return! matcherLoop list
            with e ->
                raise e
                //todo log error 
        }
        matcherLoop []
    )

let startMatch x = Add x |> agent.Post
let stopMatch x = Remove x |> agent.Post