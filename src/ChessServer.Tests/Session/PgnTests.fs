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
open FsUnit.Xunit
open TestHelper

let pgnFiles = Directory.EnumerateFiles("pgn") |> List.ofSeq
let getPgnMoves count = parse count pgnFiles
let allPgnMoves() = parseAll pgnFiles

[<Fact(Skip="too long")>]
let ``test pgn files`` () = allPgnMoves() |> ignore

[<Fact>]
let ``process pgn files on session and check correctness`` () = 
    getPgnMoves 300 
    |> Seq.toList
    //todo parallel
    |> List.iter (fun game ->
        let channels = channelInfo()
        let sw, sb = channels.CreateSession()

        let processMove session opponentNotify pgnMove =
            let primary = pgnMove.Primary
            let move = {
                Src = primary.Src
                Dst = primary.Dst
                PawnPromotion = pgnMove.PawnPromotion
            }
            try 
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
            with 
            | SessionException(_) -> 
                let notify = channels.WhiteNotify()
                match notify with 
                | EndGameNotify endGame :: _ ->
                    ()
                | _ -> ()
                reraise()


        game
        |> List.iteri (fun m row -> 
            processMove sw channels.BlackNotify row.WhiteMove
            match row.BlackMove with
            | Some move -> processMove sb channels.WhiteNotify move
            | None -> ()
        )
    )