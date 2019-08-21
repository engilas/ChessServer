module TestHelper

open FsUnit.Xunit
open System
open Xunit

let invalidArgument = typeof<ArgumentException>
let nullArgument = typeof<ArgumentNullException>

let testFunction (f: 'a -> 'b) (input: 'a) : ('b -> unit) =
    f input |> should equal

let failTest msg : 'a = 
    Assert.True(false, msg)
    Unchecked.defaultof<'a>

let debugCatch f =
    try f() |> ignore with e ->
        ()
    f

let toObjectSeq arg = arg |> Seq.map (fun x -> [| x :> obj |])