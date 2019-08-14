module ChessHelperTests

open System
open Xunit
open ChessServer.ChessHelper
open TestHelper

[<Theory>]
[<InlineData("a5", 0)>]
[<InlineData("h5", 7)>]
[<InlineData("a!", 0)>] //second symbol ignored
let ``getColumn correctness`` input output =
    let col = getColumn input
    Assert.Equal(output, col)

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
    throwsWithMessage "invalid" (fun () -> getColumn input)

[<Fact>]
let ``getColumn null throws`` () =
    throws<ArgumentNullException>(fun () -> getColumn null |> ignore)





[<Theory>]
[<InlineData("a1", 7)>]
[<InlineData("h8", 0)>]
[<InlineData("!5", 3)>] //first symbol ignored
let ``geRow correctness`` input output =
    let row = getRow input
    Assert.Equal(output, row)

[<Theory>]
[<InlineData("z0")>]
[<InlineData("i9")>]
[<InlineData("x!")>]
//invalid symbol count
[<InlineData("a")>]
[<InlineData("1")>]
[<InlineData("1bx")>]
let ``getRow invalid data`` input =
    throwsWithMessage "invalid" (fun () -> getRow input)

[<Fact>]
let ``getRow null throws`` () =
    throws<ArgumentNullException>(fun () -> getRow null |> ignore)




[<Theory>]
[<InlineData(0, "a8")>]
[<InlineData(63, "h1")>]
let ``getPosition correctness`` input output =
    let result = getPosition input
    Assert.Equal(result, output)


let positionRange() = seq {64uy..255uy} |> Seq.map (fun x -> [| x :> obj |])
[<Theory>]
[<MemberData("positionRange")>]
let ``getPosition check range`` input =
    throwsWithMessage "invalid" (fun () -> getPosition input) |> ignore