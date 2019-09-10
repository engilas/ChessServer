module PgnTests

open Xunit
open NotationParser
open System.IO
open SessionBase
open Types.Domain
open Types.Command
open Types.Channel
open System.Diagnostics
open SessionTypes

let pgnFiles = Directory.EnumerateFiles("pgn") |> List.ofSeq
let getPgnMoves count = parse count pgnFiles
let allPgnMoves() = parseAll pgnFiles

[<Fact>]
let ``test pgn files`` () = allPgnMoves() |> ignore

[<Fact>]
let ``process pgn files on session and check correctness`` () = 
    getPgnMoves 300 
    |> Seq.toList
    //todo parallel
    |> List.iter (fun game ->
        let channels = channelInfo()
        let sw, sb = channels.CreateSession()

        let processMove session pgnMove =
            let primary = pgnMove.Move.Primary
            let move = {
                Src = primary.Src
                Dst = primary.Dst
                PawnPromotion = pgnMove.Move.PawnPromotion
            }
            let result = session.CreateMove move
            match result with
            | MoveResult.Ok -> ()
            | _ -> failwith "Invalid move result"


        game
        |> List.iteri (fun m row -> 
            processMove sw row.WhiteMove
            match row.BlackMove with
            | Some move -> processMove sb move
            | None -> ()
        )
    )