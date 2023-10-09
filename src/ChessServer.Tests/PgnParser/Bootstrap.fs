[<AutoOpen>]
module internal ilf.pgn.PgnParsers.Bootstrap

open System

let toNullable =
    function
    | None -> Nullable()
    | Some x -> Nullable(x)