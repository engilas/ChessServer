module SessionMoveTests

open Types.Channel
open Xunit
open TestHelper
open SessionBase
open FsUnit.Xunit
open SessionTestData

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

let checkInvalidMove = function
| InvalidMove -> ()
| _ -> failTest "wrong move result"

let validWhiteMovesData() = validWhiteMovesData()
let validBlackMovesData() = validBlackMovesData()

[<Theory>]
[<MemberData("validWhiteMovesData")>]
let ``white move correctness`` data =
    let channels = channelInfo()
    let sw, _ = channels.CreateSession()
    makeValidMove sw data 

[<Theory>]
[<MemberData("validBlackMovesData")>]
let ``black move correctness`` data = 
    let channels = channelInfo()
    let sw, sb = channels.CreateSession()
    makeValidMove sw correctWhiteMove 
    makeValidMove sb data

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
let ``move error - not your turn - black`` () =
    let channels = channelInfo()
    let _, sb = channels.CreateSession()
    let result = sb.CreateMove correctBlackMove
    checkNotYourTurn result

[<Fact>]
let ``move error - not your turn - white`` () =
    let channels = channelInfo()
    let sw, _ = channels.CreateSession()
    makeValidMove2 sw "a2" "a4" 
    let result = sw.CreateMove correctWhiteMove
    checkNotYourTurn result

[<Theory>]
[<MemberData("validBlackMovesData")>]
let ``move error - invalid move - white`` data =
    let channels = channelInfo()
    let sw, _ = channels.CreateSession()
    sw.CreateMove data |> checkInvalidMove

[<Theory>]
[<MemberData("validWhiteMovesData")>]
let ``move error - invalid move - black`` data =
    let channels = channelInfo()
    let sw, sb = channels.CreateSession()
    makeValidMove sw <| correctWhiteMove
    sb.CreateMove data |> checkInvalidMove
    
[<Theory>]
[<InlineData("a2", "b2")>]
[<InlineData("a2", "b3")>]
[<InlineData("a3", "a4")>]
let ``move error - invalid move 2 - white`` src dst =
    let channels = channelInfo()
    let sw, _ = channels.CreateSession()
    getMove src dst |> sw.CreateMove |> checkInvalidMove
    
[<Theory>]
[<InlineData("a7", "b7")>]
[<InlineData("a7", "b6")>]
[<InlineData("a6", "a5")>]
let ``move error - invalid move 2 - black`` src dst =
    let channels = channelInfo()
    let sw, sb = channels.CreateSession()
    makeValidMove sw <| correctWhiteMove
    getMove src dst |> sb.CreateMove |> checkInvalidMove