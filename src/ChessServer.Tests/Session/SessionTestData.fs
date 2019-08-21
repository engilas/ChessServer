module SessionTestData

open SessionBase
open TestHelper

[<AutoOpen>]
module private Internal =
    type MoveForPosition = {
        Position: string
        Targets: string list
    }
    

    let prepareData (data: MoveForPosition list) =
        data
        |> List.collect (fun move -> 
            move.Targets |> List.map (fun target -> getMove move.Position target))
        |> toObjectSeq

    let validWhiteMoves = [
        //pawn
        {Position = "a2"; Targets = ["a3"; "a4"]}
        {Position = "b2"; Targets = ["b3"; "b4"]}
        {Position = "c2"; Targets = ["c3"; "c4"]}
        {Position = "d2"; Targets = ["d3"; "d4"]}
        {Position = "e2"; Targets = ["e3"; "e4"]}
        {Position = "f2"; Targets = ["f3"; "f4"]}
        {Position = "g2"; Targets = ["g3"; "g4"]}
        {Position = "h2"; Targets = ["h3"; "h4"]}

        //knight
        {Position = "b1"; Targets = ["a3"; "c3"]}
        {Position = "g1"; Targets = ["h3"; "f3"]}
    ]

    let validBlackMoves = [
        //pawn
        {Position = "a7"; Targets = ["a6"; "a5"]}
        {Position = "b7"; Targets = ["b6"; "b5"]}
        {Position = "c7"; Targets = ["c6"; "c5"]}
        {Position = "d7"; Targets = ["d6"; "d5"]}
        {Position = "e7"; Targets = ["e6"; "e5"]}
        {Position = "f7"; Targets = ["f6"; "f5"]}
        {Position = "g7"; Targets = ["g6"; "g5"]}
        {Position = "h7"; Targets = ["h6"; "h5"]}

        //knight
        {Position = "b8"; Targets = ["a6"; "c6"]}
        {Position = "g8"; Targets = ["h6"; "f6"]}
    ]

let validWhiteMovesData() = prepareData validWhiteMoves
let validBlackMovesData() = prepareData validBlackMoves