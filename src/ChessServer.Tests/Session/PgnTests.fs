module PgnTests

open Xunit
open PgnParser
open System.IO
open SessionBase
open Types.Command
open Types.Channel
open FsUnit.Xunit
open TestHelper
open FSharp.Collections.ParallelSeq
open Types.Domain

let pgnFiles = Directory.EnumerateFiles("pgn") |> List.ofSeq
let getPgnMoves count = parse count pgnFiles
let allPgnMoves() = parseAll pgnFiles

[<Fact>]
let ``test pgn files`` () = allPgnMoves() |> PSeq.toArray |> ignore

[<Fact>]
let ``process pgn files on session and check correctness`` () = 
    let moves = allPgnMoves()
    moves |> PSeq.iter (fun game -> 
        let channels = channelInfo()
        let sw, sb = channels.CreateSession()

        let processMove session opponentNotify pgnMove =
            let primary = pgnMove.Primary
            let move = {
                Src = primary.Src
                Dst = primary.Dst
                PawnPromotion = pgnMove.PawnPromotion
            }
            
            let result = session.CreateMove move
            match result with
            | MoveResult.Ok -> ()
            | _ -> failwith "Invalid move result"

            let moveDesc = 
                match opponentNotify() with
                | MoveNotify moveDesc :: _ -> moveDesc
                | EndGameNotify _ :: MoveNotify moveDesc :: _ -> moveDesc
                | _ -> failTest "Invalid notify sequence"

            moveDesc |> should equal pgnMove


        game
        |> List.iteri (fun m row -> 
            processMove sw channels.BlackNotify row.WhiteMove
            match row.BlackMove with
            | Some move -> processMove sb channels.WhiteNotify move
            | None -> ()
        )
    )