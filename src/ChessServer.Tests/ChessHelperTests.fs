module ChessHelperTests

open System
open Xunit
open ChessServer

open ChessHelper

let throwsAny f = Assert.ThrowsAny(fun () -> f() |> ignore)

[<Fact>]
let ``getColumn a returns 0`` () =
    let col = getColumn "a5"
    Assert.Equal(0uy, col)

[<Fact>]
let ``getColumn h returns 7`` () =
    let col = getColumn "h5"
    Assert.Equal(7uy, col)

[<Fact>]
let ``getColumn second symbol ignored`` () =
    let col = getColumn "a!"
    Assert.Equal(0uy, col)

[<Theory>]
[<InlineData("z1")>]
[<InlineData("i1")>]
[<InlineData("51")>]
[<InlineData("!1")>]
let ``getColumn not in range a-h throws`` input =
    throwsAny(fun () -> getColumn input) |> ignore

[<Fact>]
let ``getColumn null throws`` () =
    let ex = throwsAny(fun () -> getColumn null)
    Assert.Equal(ex.GetType(), typeof<ArgumentNullException>)

[<Theory>]
[<InlineData("a")>]
[<InlineData("1")>]
[<InlineData("1bx")>]
let ``getColumn invalid symbol count`` input =
    throwsAny(fun () -> getColumn input) |> ignore





[<Fact>]
let ``geRow 1 returns 7`` () =
    let row = getRow "a1"
    Assert.Equal(7uy, row)

[<Fact>]
let ``getRow 8 returns 0`` () =
    let col = getRow "h8"
    Assert.Equal(0uy, col)

[<Fact>]
let ``getRow first symbol ignored`` () =
    let col = getRow "!5"
    Assert.Equal(3uy, col)

[<Theory>]
[<InlineData("z0")>]
[<InlineData("i9")>]
[<InlineData("x!")>]
let ``getRow not in range 1-8 throws`` input =
    throwsAny(fun () -> getRow input) |> ignore

[<Fact>]
let ``getRow null throws`` () =
    let ex = throwsAny(fun () -> getRow null |> ignore)
    Assert.Equal(ex.GetType(), typeof<ArgumentNullException>)

[<Theory>]
[<InlineData("a")>]
[<InlineData("1")>]
[<InlineData("1bx")>]
let ``getRow invalid symbol count`` input =
    throwsAny(fun () -> getRow input) |> ignore




[<Fact>]
let ``getPosition 0 returns a8`` () =
    let result = getPosition 0uy
    Assert.Equal(result, "a8")

let positionRange() = seq {65uy..255uy} |> Seq.map (fun x -> [| x :> obj |])

[<Theory>]
[<MemberData("positionRange")>]
let ``getPosition check range`` input =
    throwsAny(fun () -> getPosition input) |> ignore