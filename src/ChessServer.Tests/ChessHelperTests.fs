module ChessHelperTests

open Xunit
open ChessHelper
open TestHelper
open FsUnit.Xunit

[<Theory>]
[<InlineData("a5", 0uy)>]
[<InlineData("h5", 7uy)>]
[<InlineData("a!", 0uy)>] //second symbol ignored
let ``getColumn correctness`` i o = testFunction getColumn i o

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
    (fun () -> getColumn input) |> should throw invalidArgument

[<Fact>]
let ``getColumn null throws`` () =
    (fun () -> getColumn null) |> should throw nullArgument





[<Theory>]
[<InlineData("a1", 7uy)>]
[<InlineData("h8", 0uy)>]
[<InlineData("!5", 3uy)>] //first symbol ignored
let ``getRow correctness`` i o = testFunction getRow i o

[<Theory>]
[<InlineData("z0")>]
[<InlineData("i9")>]
[<InlineData("x!")>]
//invalid symbol count
[<InlineData("a")>]
[<InlineData("1")>]
[<InlineData("1bx")>]
let ``getRow invalid data`` input =
    (fun () -> getRow input) |> should throw invalidArgument

[<Fact>]
let ``getRow null throws`` () =
    (fun () -> getRow null) |> should throw nullArgument




[<Theory>]
[<InlineData(0uy, "a8")>]
[<InlineData(63uy, "h1")>]
let ``positionToString correctness`` i o = testFunction positionToString i o

[<Theory>]
[<InlineData("a8", 0uy)>]
[<InlineData("h1", 63uy)>]
let ``parsePosition correctness`` i o = testFunction parsePosition i o


let positionRange() = seq {64uy..255uy} |> toObjectSeq
[<Theory>]
[<MemberData("positionRange")>]
let ``positionToString check range`` input =
    (fun () -> positionToString input) |> should throw invalidArgument