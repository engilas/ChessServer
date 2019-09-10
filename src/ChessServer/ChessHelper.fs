module ChessHelper

open System
open Types.Domain
open EngineMappers
open ChessEngine.Engine
open Helper

let getColumn (input:string) =
    if input = null then
        raise (ArgumentNullException("input"))

    match input.Length with
    | 2 ->
        let col = input.Substring(0, 1).ToLowerInvariant().[0]
        if col < 'a' || col > 'h' then
            invalidArg "input" input "column not in range (a-h)"
            
        byte col - byte 'a'
    | _ -> invalidArg "input" input "wrong string length"

let getRow (input:string) =
    if input = null then nullArg "input"

    match input.Length with
    | 2 -> 
        let isNumber, row = input.Substring(1, 1).ToLowerInvariant() |> Int32.TryParse
        if not isNumber then 
            invalidArg "input" input "row is not a number" 
        if row < 1 || row > 8 then
            invalidArg "input" input "row not in range (1-8)"
        8 - (input.Substring(1, 1) |> Int32.Parse) |> byte
    | _ -> invalidArg "input" input "wrong string length '%s'"

let positionFromString (pos: string) =
    let col = getColumn pos
    let row = getRow pos
    col + row * 8uy

let positionToString (pos: byte) =
    if pos >= 64uy then 
        invalidArg "pos" pos "not in range (1-64)"
        
    let pos = int pos
    let col = pos % 8
    let row = pos / 8
    ((int 'a' + col) |> char |> string) + (8 - row).ToString()
        
let moveAction src dst = {Src = src; Dst = dst}

let checkEndGame (engine: Engine) =
    if engine.StaleMate then
        if engine.InsufficientMaterial then
            (Draw, "Draw by insufficient material") |> Some
        //elif engine.RepeatedMove then
        //    (Draw, "Draw by repetition") |> Some
        //elif engine.FiftyMove then
        //    (Draw, "Draw by fifty move rule") |> Some
        // enable later by game params
        else
            (Draw, "Stalemate") |> Some
    elif (engine.GetWhiteMate()) then
        (BlackWin, "Black mates") |> Some
    elif (engine.GetBlackMate()) then
        (WhiteWin, "White mates") |> Some
    else None
        

let getMoveDescriptionFromEngine (engine: Engine) = 
    let getMoveAction (x: PieceMoving) = moveAction x.SrcPosition x.DstPosition
    let ifExists (x:ChessPieceType) value =
        match x with
        | ChessPieceType.None -> None
        | _ -> Some value

    let lastMove = engine.LastMove
    let takenPiece = ifExists lastMove.TakenPiece.PieceType
                        <| positionToString lastMove.TakenPiece.Position
                    
    let move = getMoveAction lastMove.MovingPiecePrimary
    let secondMove = ifExists lastMove.MovingPieceSecondary.PieceType
                        <| getMoveAction lastMove.MovingPieceSecondary
    let pawnPromoted = ifExists lastMove.PawnPromotedTo lastMove.PawnPromotedTo

    { Primary = move; Secondary = secondMove; TakenPiecePos = takenPiece; PawnPromotion = fromEngineType pawnPromoted }