module PgnTests

open Xunit
open NotationParser
open System.IO
open SessionBase
open Types.Domain
open Types.Command
open Types.Channel

//let pgnFiles = Directory.EnumerateFiles("pgn")
let getMovesFromPgn = 
    Directory.EnumerateFiles("pgn")
    |> Seq.map parse
    |> Seq.collect id

[<Fact>]
let ``test pgn files`` () =
    getMovesFromPgn
    |> Seq.take 300
    |> Seq.toList
    //todo parallel
    |> ignore

[<Fact>]
let ``process pgn files on session and check correctness`` () = 
    getMovesFromPgn 
    |> Seq.take 300 
    |> Seq.toList
    //todo parallel
    |> List.iteri (fun i game ->
        let channels = channelInfo()
        let sw, sb = channels.CreateSession()
        let getSession = function White -> sw | Black -> sb

        game
        |> List.map (fun row ->
            [White, Some row.WhiteMove; Black, row.BlackMove]
            |> List.filter (fun (_, move) -> move.IsSome)
            |> List.map (fun (color, move) -> color, move.Value)
        )
        |> List.collect id
        |> List.iteri (fun i (color, pgnMove) -> 
            let session = getSession color
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
        )
    )