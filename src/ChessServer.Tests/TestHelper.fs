module TestHelper

open Xunit
open System

let throwsAnyM f msg = 
    let e = Assert.ThrowsAny(fun () -> f() |> ignore)
    Assert.Contains(msg, e.Message, StringComparison.OrdinalIgnoreCase)