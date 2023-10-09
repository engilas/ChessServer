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
open ilf.pgn.Data
open ChessServer.Common.ChessHelper

//[<Fact(Skip="too long")>]
[<Fact>]
let ``test pgn files`` () = allPgnMoves() |> PSeq.toArray |> ignore

//[<Fact(Skip="too long")>]
[<Fact>]
let ``process pgn files on session and check correctness`` () = 
    let moves = allPgnMoves()
    moves |> PSeq.iter (fun game -> 
        let channels = channelInfo()
        let sw, sb = channels.CreateSession()

        let processMove session opponentNotify (move: IlfMove) =
            let parserdMove = moveAction (move.OriginSquare.ToString() |> positionFromString) (move.TargetSquare.ToString() |> positionFromString)
            let promoted = 
                if move.PromotedPiece.HasValue then
                    match move.PromotedPiece.Value with
                    | PieceType.Bishop -> Some Bishop
                    | PieceType.King -> Some King
                    | PieceType.Queen -> Some Queen
                    | PieceType.Rook -> Some Rook
                    | PieceType.Knight -> Some Knight
                    | PieceType.Pawn -> Some Pawn
                    | _ -> failwithf "unknown type %A" move.PromotedPiece.Value
                else None
            let moveCommand = {
                Move = parserdMove
                PawnPromotion = promoted
            }
            
            let result = session.CreateMove moveCommand
            match result with
            | Ok _ -> ()
            | _ -> failwith "Invalid move result"

            let moveDesc = 
                match opponentNotify() with
                | MoveNotify moveDesc :: _ -> moveDesc
                | EndGameNotify _ :: MoveNotify moveDesc :: _ -> moveDesc
                | _ -> failTest "Invalid notify sequence"

            moveDesc.Primary |> should equal parserdMove
            moveDesc.Check |> should equal move.IsCheck
            moveDesc.Mate |> should equal move.IsCheckMate
            moveDesc.PawnPromotion |> should equal promoted


        game
        |> List.iteri (fun m row -> 
            let color, move = row
            match color with
            | White -> processMove sw channels.Black.GetNotify move
            //let firstConn, secondConn = channels.White, channels.Black
            //let firstSession, secondSession = sw, sb
            //processMove sw secondConn.GetNotify row
            //match row.BlackMove with
            //| Some move -> processMove sb channels.White.GetNotify move
            //| None -> ()
        )
    )