module TestHelper

open Xunit
open System

let testEqual<'a> (a:'a) (b:'a) = Assert.Equal(a, b)



let private checkExceptionContains s (e:Exception) =
    Assert.Contains(s, e.Message, StringComparison.OrdinalIgnoreCase)

let throwsAny f = Assert.ThrowsAny(fun () -> f() |> ignore)
let throws<'e when 'e:> exn> (f: unit -> unit) = Assert.Throws<'e>(fun () -> f())
let throwsExactOrDerived<'e when 'e:> exn> f = Assert.ThrowsAny<'e>(fun () -> f())

let throwsAnyWithMessage msg f = 
    let e = Assert.ThrowsAny(fun () -> f() |> ignore)
    checkExceptionContains msg e
    e

let throwsWithMessage<'e when 'e:> exn> msg f = 
    let e = Assert.Throws<'e>(fun () -> f() |> ignore)
    checkExceptionContains msg e
    e

let throwsInvalidArg f = Assert.Throws<ArgumentException>(fun () -> f() |> ignore)
let throwsInvalidArgWithMessage msg f = 
    let e = Assert.Throws<ArgumentException>(fun () -> f() |> ignore)
    checkExceptionContains msg e
    e

let throwsNullArg f = Assert.Throws<ArgumentNullException>(fun () -> f() |> ignore)
