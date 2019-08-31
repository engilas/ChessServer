module PgnTests

open Xunit
open NotationParser
open System.IO

[<Fact>]
let ``test pgn files`` () =
    let pgnFiles = Directory.EnumerateFiles("pgn")
    pgnFiles
    |> Seq.map (fun f -> parse f)
    |> ignore
    ()