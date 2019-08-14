module TestHelper

open Xunit
open System

let throwsAny f = Assert.ThrowsAny(fun () -> f() |> ignore)
let throws<'e when 'e:> exn> (f: unit -> unit) = Assert.Throws<'e>(fun () -> f())
let throwsExactOrDerived<'e when 'e:> exn> f = Assert.ThrowsAny<'e>(fun () -> f())

let throwsWithMessage msg f = 
    let e = Assert.ThrowsAny(fun () -> f() |> ignore)
    Assert.Contains(msg, e.Message, StringComparison.OrdinalIgnoreCase)
    e