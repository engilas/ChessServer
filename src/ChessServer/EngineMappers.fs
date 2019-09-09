module EngineMappers

open ChessEngine.Engine
open Types.Domain

let mapperFun fst snd x = snd << List.find (fst >> ((=) x))

let private colorMap fst snd x = 
    [
        ChessPieceColor.White, White
        ChessPieceColor.Black, Black
    ] |> mapperFun fst snd x

let private typeMap fst snd x = 
    [
        ChessPieceType.King, King
        ChessPieceType.Queen, Queen
        ChessPieceType.Rook, Rook
        ChessPieceType.Bishop, Bishop
        ChessPieceType.Knight, Knight
        ChessPieceType.Pawn, Pawn
    ] |> mapperFun fst snd x 

let fromEngineType = function
| None -> None
| Some x -> x |> function
    | ChessPieceType.None -> None
    | x -> x |> typeMap fst snd |> Some

let toEngineType = typeMap snd fst

let fromEngineColor = colorMap fst snd
let toEngineColor = colorMap snd fst