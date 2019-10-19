module ChessServer.Tests.Session.PgnTests

open Xunit
open ChessServer
open ChessServer.Tests
open ChessServer.Common
open PgnParser
open SessionBase
open Types.Command
open Types.Channel
open FsUnit.Xunit
open FSharp.Collections.ParallelSeq
open Types.Domain

[<Fact(Skip="too long")>]
let ``test pgn files`` () = allPgnMoves() |> PSeq.toArray |> ignore

[<Fact(Skip="too long")>]
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
            | Ok _ -> ()
            | _ -> failwith "Invalid move result"

            let moveDesc = 
                match opponentNotify() with
                | MoveNotify moveDesc :: _ -> moveDesc
                | EndGameNotify _ :: MoveNotify moveDesc :: _ -> moveDesc
                | _ -> failTest "Invalid notify sequence"

            moveDesc |> should equal pgnMove


        game
        |> List.iteri (fun m row -> 
            processMove sw channels.Black.GetNotify row.WhiteMove
            match row.BlackMove with
            | Some move -> processMove sb channels.White.GetNotify move
            | None -> ()
        )
    )