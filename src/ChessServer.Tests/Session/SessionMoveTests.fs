module SessionMoveTests

open Types.Channel
open Types.Command
open Xunit
open TestHelper
open SessionBase

[<Fact>]
let ``white move`` () = async {
    let sw, _ = channels.CreateSession()
    let! result = sw.CreateMove {moveStub with From = "a2"; To = "a4"}
    match result with
    | Ok -> ()
    | _ -> failTest "wrong move result"
}

[<Fact>]
let ``move error - not your turn (black)`` () = async {
    let channels = channelInfo()

    let _, sb = channels.CreateSession()
    let! result = sb.CreateMove {moveStub with From = "a7"; To = "a5"}

    match result with
    | NotYourTurn -> ()
    | _ -> failTest "wrong move result"
}

[<Fact>]
let ``move error - not your turn (white)`` () = async {
    let channels = channelInfo()

    let sw, _ = channels.CreateSession()
    let! result = sw.CreateMove {moveStub with From = "a2"; To = "a4"}

    match result with
    | Ok -> ()
    | _ -> failTest "wrong move result"

    let! result = sw.CreateMove {moveStub with From = "b2"; To = "b4"}

    match result with
    | NotYourTurn -> ()
    | _ -> failTest "wrong move result"
}