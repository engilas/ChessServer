module SessionMoveTests

open Types.Channel
open Types.Command
open Xunit
open TestHelper
open SessionBase
open FsUnit.Xunit

let getMove from _to = {moveStub with From = from; To = _to}

let makeValidMove session move = 
    session.CreateMove move
    |> function
    | Ok -> ()
    | _ -> failTest "wrong move result"

let makeValidMove2 session from _to = getMove from _to |> makeValidMove session

let correctWhiteMove = getMove "a2" "a4"
let correctBlackMove = getMove "a7" "a5"

let checkNotYourTurn = function
| NotYourTurn -> ()
| _ -> failTest "wrong move result"

[<Theory>]
[<InlineData("a2", "a4")>]
[<InlineData("b2", "b4")>]
[<InlineData("h2", "h3")>]
[<InlineData("b1", "c3")>]
let ``white move correctness`` from _to = async {
    let channels = channelInfo()
    let sw, _ = channels.CreateSession()
    makeValidMove2 sw from _to 
}

[<Theory>]
[<InlineData("a7", "a5")>]
[<InlineData("b7", "b5")>]
[<InlineData("h7", "h6")>]
[<InlineData("b8", "c6")>]
let ``black move correctness`` from _to = async {
    let channels = channelInfo()
    let sw, sb = channels.CreateSession()
    makeValidMove sw correctWhiteMove 
    makeValidMove2 sb from _to 
}

[<Theory>]
[<InlineData("a2x")>]
[<InlineData("a")>]
[<InlineData("22")>]
[<InlineData("2a")>]
let ``move error - invalid value`` data =
    let channels = channelInfo()
    let sw, _ = channels.CreateSession()
    let checkMoveError source from _to =
        let result = sw.CreateMove <| getMove from _to
        match result with
        | InvalidInput msg -> msg |> should haveSubstring source
        | _ -> failTest "wrong move result"

    checkMoveError "From" data "a4"
    checkMoveError "To" "a4" data

[<Fact>]
let ``move error - not your turn (black)`` () =
    let channels = channelInfo()
    let _, sb = channels.CreateSession()
    let result = sb.CreateMove correctBlackMove
    checkNotYourTurn result

[<Fact>]
let ``move error - not your turn (white)`` () =
    let channels = channelInfo()
    let sw, _ = channels.CreateSession()
    makeValidMove2 sw "a2" "a4" 
    let result = sw.CreateMove correctWhiteMove
    checkNotYourTurn result