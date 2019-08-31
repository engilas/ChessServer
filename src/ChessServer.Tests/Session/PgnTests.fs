module PgnTests

open Xunit
open NotationParser
open System.IO

[<Fact>]
let ``test pgn files`` () =
    let pgnFiles = Directory.EnumerateFiles("pgn") |> List.ofSeq
    pgnFiles
    |> List.map (fun f -> parse f)
    |> ignore
    ()