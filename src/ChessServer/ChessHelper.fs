namespace ChessServer

module ChessHelper = 
    open System

    let getColumn (input:string) =
        if input = null then
            raise (ArgumentNullException("input"))

        match input.Length with
        | 2 ->
            let col = input.Substring(0, 1).ToLowerInvariant().[0]
            if col < 'a' || col > 'h' then
                failwithf "Invalid argument '%s' - column not in range" input
            
            byte col - byte 'a'
        | _ -> failwithf "Invalid input string length '%s'" input

    let getRow (input:string) =
        if input = null then
            raise (ArgumentNullException("input"))

        match input.Length with
        | 2 -> 8 - (input.Substring(1, 1) |> Int32.Parse) |> byte
        | _ -> failwithf "Invalid input string length '%s'" input
        
        

