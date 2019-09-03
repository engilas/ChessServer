﻿module NotationParser

open Types.Domain
open System
open System.IO
open System.Text.RegularExpressions
open ChessEngine.Engine
open ChessHelper
open EngineMappers
open FsUnit.Xunit

type NotationMove = {
    Piece: PieceType
    ColSrc: byte
    RowSrc: byte
    ColDst: byte
    RowDst: byte
}

type NotationRow = {
    WhiteMove: NotationMove
    BlackMove: NotationMove
}

[<AutoOpen>]
module private Internal =
    let pieceMap = [
        "K", King
        "Q", Queen
        "R", Rook
        "B", Bishop
        "N", Knight
    ]
    let pieceNames = pieceMap |> List.map fst
    let getPieceType x = pieceMap |> List.find (fun (s, _) -> s = x) |> snd

    let files = ['a'..'h'] |> List.map string
    let ranks = ['1'..'8'] |> List.map string
    let contains (y: string) (x: string) = x.Contains(y)
    let removeLast (y: string) (x: string) =
        let idx = x.LastIndexOf(y)
        if idx = -1 then x else x.Remove(idx, y.Length)
    let check y x = removeLast y x, contains y x

    let isTake = check "x"
    let isCheck = check "+"
    let isMate = check "#"
    let isLongCastling = check "O-O-O"

    let isShortCastling x =
        let (x, isLong) = isLongCastling x
        if isLong then (x, false)
        else check "O-O" x

    let getTarget (x:string) =
        if x.Length < 2 then x, None
        else
            let lastTwo = x.Substring(x.Length - 2, 2)
            let pos = parsePosition lastTwo
            removeLast lastTwo x, Some pos
        
    let getPromotion (x:string) =
        let possibleValues = pieceNames |> List.map ((+) "=")
        let promotion = possibleValues |> List.tryFind (fun p -> contains p x)
        match promotion with
        | Some value -> 
            removeLast value x, removeLast "=" value |> getPieceType |> Some
        | None -> x, None

    let getSourceHints (x:string) =
        let possibleRank = ranks |> List.tryFind(fun v -> contains v x)
        let possibleFile = files |> List.tryFind(fun v -> contains v x)
        let possiblePiece = pieceNames |> List.tryFind(fun v -> contains v x)

        let tryRemove value (x:string)  =
            match value with
            | Some v -> removeLast v x
            | None -> x

        let x = x |> (tryRemove possibleRank >> tryRemove possibleFile >> tryRemove possiblePiece)

        let possiblePiece = 
            match possiblePiece with
            | Some x -> getPieceType x
            | None -> Pawn

        (x, possiblePiece, possibleFile, possibleRank)

    let makeMoveAN (engine: Engine) (move:string) =
        let valid = engine.IsValidMoveAN(move)
        let isValidMove = valid && engine.MovePieceAN(move)
        if not isValidMove then failwith "invalid move"

    let checkCastline (engine: Engine) engineColor = 
        let first = engine.LastMove.MovingPiecePrimary
        let second = engine.LastMove.MovingPieceSecondary
        first.PieceColor |> should equal engineColor
        first.PieceType |> should equal ChessPieceType.King
        second.PieceColor |> should equal engineColor
        second.PieceType |> should equal ChessPieceType.Rook

    let processMove (engine: Engine) move color =
        let move1, longCast = isLongCastling move
        let move2, shortCast = isShortCastling move1
        let move3, check = isCheck move2
        let move4, mate = isMate move3
        let move5, promotion = getPromotion move4
        let move6, target = getTarget move5
        let move7, take = isTake move6
        let move8, piece, file, rank = getSourceHints move7

        if not <| String.IsNullOrWhiteSpace(move8) then failwith "parse error"

        let makeMoveAN = makeMoveAN engine
        let engineColor = toEngineColor color
        let engineType = toEngineType piece
        let castRank = match color with White -> "1" | Black -> "8"

        if longCast then
            makeMoveAN <| sprintf "e%sc%s" castRank castRank
            checkCastline engine engineColor
        elif shortCast then
            makeMoveAN <| sprintf "e%sg%s" castRank castRank
            checkCastline engine engineColor
        elif target.IsSome then
            let target = target.Value

            match promotion with
            | Some p -> p |> toEngineType |> (fun x -> engine.PromoteToPieceType <- x)
            | None -> ()

            let unwrap f g x =
                match x with
                | Some x -> x |> (g >> f >> int)
                | None -> -1

            let col = unwrap getColumn (fun s -> sprintf "%s_" s) file
            let row = unwrap getRow (fun s -> sprintf "_%s" s) rank

            if not <| engine.MovePiece(engineType, engineColor, target, take, col, row) 
            then failwith "invalid move"

            // asserts
            let lastMove = engine.LastMove
            lastMove.MovingPieceSecondary.PieceType |> should equal ChessPieceType.None
            let move = lastMove.MovingPiecePrimary
            move.PieceType |> should equal engineType
            move.PieceColor |> should equal engineColor

            match promotion with
            | Some p -> lastMove.PawnPromotedTo |> should equal (p |> toEngineType)
            | None -> ()

            if take then lastMove.TakenPiece.PieceType |> should not' (equal ChessPieceType.None)
            
            let checkMates = 
                engine.GetBlackCheck(), engine.GetBlackMate(), engine.GetWhiteCheck(), engine.GetWhiteMate()

            match color with
            | White -> checkMates |> should equal (check || mate, mate, false, false)
            | Black -> checkMates |> should equal (false, false, check || mate, mate)

            ()
        else
            failwith "parse error"

let parseGame (lines: string list) =
    let lines =
        lines
        |> List.skipWhile (fun x ->
            x.StartsWith('[')
        )
    let text = String.Join(' ', lines)
    let scorePossibleValues = [ "1-0"; "0-1"; "1/2-1/2"; "*" ]
    let score = 
        scorePossibleValues |> List.find (fun x -> text.Contains(x))
    let commentsRegex = new Regex("{.*}")
    let text = commentsRegex.Replace(text, "").Replace(score, "")
    let splitRegex = new Regex(@"\d+\.")
    let moves = 
        splitRegex.Split(text)
        |> List.ofSeq
        |> List.filter(fun x -> not <| String.IsNullOrWhiteSpace(x))
        |> List.map(fun x ->
            match x.Split(([||]: string[]), StringSplitOptions.RemoveEmptyEntries) |> List.ofSeq with
            | fst::snd::[] ->
                fst.Trim(), snd.Trim()
            | fst::[] ->
                fst.Trim(), null
            | _ -> failwith "incorrect round"
        )

    let engine = Engine()

    let processMove = processMove engine

    moves
    |> List.mapi(fun i (white, black) ->
        processMove white White
        if black <> null then
            processMove black Black
    ) |> ignore

let parse file =
    let text = File.ReadAllText(file)
    let splitRegex = new Regex(@"^\s*\n\s*\n", RegexOptions.Multiline)

    splitRegex.Split(text) 
    |> List.ofArray
    |> List.filter (not << String.IsNullOrWhiteSpace)
    |> List.map (fun game ->
        game.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries) |> List.ofArray
    )
    |> Array.ofList
    |> Array.Parallel.mapi (fun i game ->
        parseGame game
    )