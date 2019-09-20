[<AutoOpen>]
module TestHelper

open FsUnit.Xunit
open System
open Xunit



let failTestf format =
    Printf.ksprintf (fun s ->
        Assert.True(false, s)
        failwith ""    
    ) format

let failTest msg = failTestf "%s" msg

let inline equals expected actual =
    if expected <> actual then failTestf "Expected: %A, was: %A" expected actual

let invalidArgument = typeof<ArgumentException>
let nullArgument = typeof<ArgumentNullException>

let testFunction f input =
    f input |> should equal

let debugCatch f =
    try f() |> ignore with e ->
        ()
    f

let toObjectSeq arg = arg |> Seq.map (fun x -> [| x :> obj |])