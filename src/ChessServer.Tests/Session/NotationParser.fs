module NotationParser

open Types.Domain
open System
open System.IO
open System.Text.RegularExpressions
open ChessEngine.Engine
open ChessHelper

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
    let check y (x:string) = x.Replace(y, ""), contains y x

    let isTake = check "x"
    let isCheck = check "+"
    let isMate = check "#"
    let isLongCastling = check "O-O-O"

    let isShortCastling x =
        let (x, isLong) = isLongCastling x
        if isLong then (x, isLong)
        else check "O-O" x

    let getTarget (x:string) =
        if x.Length < 2 then x, None
        else
            let lastTwo = x.Substring(x.Length - 2, 2)
            let pos = parsePosition lastTwo
            x.Replace(lastTwo, ""), Some pos
        
    let getPromotion (x:string) =
        let possibleValues = pieceNames |> List.map ((+) "=")
        let promotion = possibleValues |> List.tryFind (fun p -> contains p x)
        match promotion with
        | Some value -> 
            x.Replace(value, ""), value.Replace("=", "") |> getPieceType |> Some
        | None -> x, None

    let getSourceHints (x:string) =
        let possibleRank = ranks |> List.tryFind(fun v -> contains v x)
        let possibleFile = files |> List.tryFind(fun v -> contains v x)
        let possiblePiece = pieceNames |> List.tryFind(fun v -> contains v x)

        let tryRemove value (x:string)  =
            match value with
            | Some v -> x.Replace(v, "")
            | None -> x

        let x = x |> (tryRemove possibleRank >> tryRemove possibleFile >> tryRemove possiblePiece)

        let possiblePiece = 
            match possiblePiece with
            | Some x -> getPieceType x
            | None -> Pawn

        (x, possiblePiece, possibleFile, possibleRank)

    let makeMove (engine: Engine) move =
        let isValidMove =
            engine.IsValidMoveAN(move)
            && engine.MovePieceAN(move)

        if not isValidMove then failwith "invalid move"

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

        let makeMove = makeMove engine

        if longCast then
            let rank = match color with White -> "1" | Black -> "8"
            makeMove <| sprintf "e%sc%s" rank rank
            let lastMove = engine.LastMove
            let first = lastMove.MovingPiecePrimary
            //let engineColor =
            //first.PieceColor 
            ()

        ()

    

let parse file =
    let lines = File.ReadAllLines(file) |> List.ofArray
    // skip tags
    let lines =
        lines
        |> List.skipWhile (fun x ->
            x.StartsWith('[')
        )
    let text = lines |> List.reduce (+)
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
                (fst.Trim(), snd.Trim())
            | fst::[] ->
                (fst.Trim(), null)
            | _ -> failwith "incorrect round"
        )

    let engine = Engine()

    let processMove = processMove engine

    moves
    |> List.map(fun (white, black) ->
        try processMove white White
        with e -> 
            ()
        try processMove black Black
        with e ->
            ()
    ) |> ignore

    
    ()