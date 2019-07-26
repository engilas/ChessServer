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

[<Fact>]
let ``getColumn not in range a-h throws`` () =
    throwsAny(fun () -> getColumn "z1") |> ignore
    throwsAny(fun () -> getColumn "i1") |> ignore
    throwsAny(fun () -> getColumn "51") |> ignore
    throwsAny(fun () -> getColumn "!1") |> ignore
    0

[<Fact>]
let ``getColumn null throws`` () =
    let ex = throwsAny(fun () -> getColumn null)
    Assert.Equal(ex.GetType(), typeof<ArgumentNullException>)

[<Fact>]
let ``getColumn invalid symbol count`` () =
    throwsAny(fun () -> getColumn "a") |> ignore
    throwsAny(fun () -> getColumn "1") |> ignore
    throwsAny(fun () -> getColumn "1bx") |> ignore





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
    let input = "!5"
    let col = getRow input
    Assert.Equal(3uy, col)

[<Fact>]
let ``getRow not in range 1-8 throws`` () =
    Assert.ThrowsAny(fun () -> getRow "z0" |> ignore) |> ignore
    Assert.ThrowsAny(fun () -> getRow "i9" |> ignore) |> ignore
    Assert.ThrowsAny(fun () -> getRow "x!" |> ignore) |> ignore
    0

[<Fact>]
let ``getRow null throws`` () =
    let ex = Assert.ThrowsAny(fun () -> getRow null |> ignore)
    Assert.Equal(ex.GetType(), typeof<ArgumentNullException>)

[<Fact>]
let ``getRow invalid symbol count`` () =
    Assert.ThrowsAny(fun () -> getRow "a" |> ignore) |> ignore
    Assert.ThrowsAny(fun () -> getRow "1" |> ignore) |> ignore
    Assert.ThrowsAny(fun () -> getRow "1bx" |> ignore) |> ignore




[<Fact>]
let ``getPosition 0 returns a8`` () =
    let result = getPosition 0uy
    Assert.Equal(result, "a8")

[<Fact>]
let ``getPosition check range`` () =
    Assert.ThrowsAny(fun () -> getPosition 100uy)
    Assert.Equal(result, "a8")