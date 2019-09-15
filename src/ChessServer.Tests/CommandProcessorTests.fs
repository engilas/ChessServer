module CommandProcessorTests

open FsUnit
open Xunit
open CommandProcessor
open TestHelper
open Types.Channel
open Types.Command

[<Fact>]
let ``test change state``() = async {
    let query, channel = createClientChannel "1" (fun _ -> ())
    channel.ChangeState Matching
    let! response = query MatchCommand channel
    match response with
    | Some (ErrorResponse {Message=msg}) -> msg |> equals "Already matched"
    | x -> failTestf "Invalid response %A" x
}
    