module ChessHelperTests

open System
open Xunit
open ChessServer.ChessHelper
open TestHelper

let testFunction f input =
    f input |> testEqual 


[<Theory>]
[<InlineData("a5", 0)>]
[<InlineData("h5", 7)>]
[<InlineData("a!", 0)>] //second symbol ignored
let ``getColumn correctness`` = testFunction getColumn

[<Theory>]
[<InlineData("z1")>]
[<InlineData("i1")>]
[<InlineData("51")>]
[<InlineData("!1")>]
//invalid symbol count
[<InlineData("a")>]
[<InlineData("1")>]
[<InlineData("1bx")>]
let ``getColumn invalid data`` input =
    throwsInvalidArg (fun () -> getColumn input) |> ignore

[<Fact>]
let ``getColumn null throws`` () =
    throwsNullArg (fun () -> getColumn null |> ignore) |> ignore





[<Theory>]
[<InlineData("a1", 7)>]
[<InlineData("h8", 0)>]
[<InlineData("!5", 3)>] //first symbol ignored
let ``getRow correctness`` = testFunction getRow

[<Theory>]
[<InlineData("z0")>]
[<InlineData("i9")>]
[<InlineData("x!")>]
//invalid symbol count
[<InlineData("a")>]
[<InlineData("1")>]
[<InlineData("1bx")>]
let ``getRow invalid data`` input =
    throwsInvalidArg (fun () -> getRow input) |> ignore

[<Fact>]
let ``getRow null throws`` () =
    throwsNullArg (fun () -> getRow null |> ignore) |> ignore




[<Theory>]
[<InlineData(0, "a8")>]
[<InlineData(63, "h1")>]
let ``getPosition correctness`` = testFunction getPosition


let positionRange() = seq {64uy..255uy} |> Seq.map (fun x -> [| x :> obj |])
[<Theory>]
[<MemberData("positionRange")>]
let ``getPosition check range`` input =
    throwsInvalidArg (fun () -> getPosition input) |> ignore