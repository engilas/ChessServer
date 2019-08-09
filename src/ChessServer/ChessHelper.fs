namespace ChessServer

module ChessHelper = 
    open System
    open CommandTypes
    open DomainTypes
    open ChessEngine.Engine

    let getColumn (input:string) =
        if input = null then
            raise (ArgumentNullException("input"))

        match input.Length with
        | 2 ->
            let col = input.Substring(0, 1).ToLowerInvariant().[0]
            if col < 'a' || col > 'h' then
                failwithf "Invalid argument '%s' - column not in range (a-h)" input
            
            byte col - byte 'a'
        | _ -> failwithf "Invalid input string length '%s'" input

    let getRow (input:string) =
        if input = null then
            raise (ArgumentNullException("input"))

        match input.Length with
        | 2 -> 
            let row = input.Substring(1, 1).ToLowerInvariant() |> int
            if row < 1 || row > 8 then
                failwithf "Invalid argument '%s' - row not in range (1-8)" input
            8 - (input.Substring(1, 1) |> Int32.Parse) |> byte
        | _ -> failwithf "Invalid input string length '%s'" input

    let getPosition (pos: byte) =
        if pos >= 64uy then 
            failwithf "Invalid argument: '%d' not in range (1-64)" pos
        
        let pos = int pos
        let col = pos % 8
        let row = pos / 8
        ((int 'a' + col) |> char |> string) + (8 - row).ToString()
        
    let moveAction from _to = {From = from; To = _to}

    let checkEndGame (engine: Engine) =
        if engine.StaleMate then
            if engine.InsufficientMaterial then
                (Draw, "Draw by insufficient material") |> Some
            elif engine.RepeatedMove then
                (Draw, "Draw by repetition") |> Some
            elif engine.FiftyMove then
                (Draw, "Draw by fifty move rule") |> Some
            else
                (Draw, "Stalemate") |> Some
        elif (engine.GetWhiteMate()) then
            (BlackWin, "Black mates") |> Some
        elif (engine.GetBlackMate()) then
            (WhiteWin, "White mates") |> Some
        else None
        

