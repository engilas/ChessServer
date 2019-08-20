module SessionMoveTests

open Types.Channel
open Types.Command
open Xunit
open TestHelper
open SessionBase
open FsUnit.Xunit

let move session from _to = 
    let result = session.CreateMove {moveStub with From = from; To = _to}
    match result with
    | Ok -> ()
    | _ -> failTest "wrong move result"

let checkNotYourTurn = function
| NotYourTurn -> ()
| _ -> failTest "wrong move result"

[<Theory>]
[<InlineData("a2x")>]
[<InlineData("a")>]
[<InlineData("22")>]
[<InlineData("2a")>]
let ``move error - invalid value`` data =
    let channels = channelInfo()
    let sw, _ = channels.CreateSession()
    let checkMoveError source move =
        let result = sw.CreateMove move
        match result with
        | InvalidInput msg -> msg |> should haveSubstring source
        | _ -> failTest "wrong move result"

    checkMoveError "From" {moveStub with From = data; To = "a4"}
    checkMoveError "To" {moveStub with From = "a4"; To = data}

[<Fact>]
let ``white move`` () =
    let sw, _ = channels.CreateSession()
    move sw "a2" "a4"

[<Fact>]
let ``move error - not your turn (black)`` () =
    let channels = channelInfo()
    let _, sb = channels.CreateSession()
    let result = sb.CreateMove {moveStub with From = "a7"; To = "a5"}
    checkNotYourTurn result

[<Fact>]
let ``move error - not your turn (white)`` () =
    let channels = channelInfo()
    let sw, _ = channels.CreateSession()
    move sw "a2" "a4"
    let result = sw.CreateMove {moveStub with From = "b2"; To = "b4"}
    checkNotYourTurn result