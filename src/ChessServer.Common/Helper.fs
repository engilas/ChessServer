module ChessServer.Common.Helper

let invalidArg argName value msg = invalidArg argName <| sprintf "'%A': %s" value msg

let tryRemove f lst =
    match lst |> List.tryFind f with
    | Some _ -> lst |> List.filter (fun x -> not <| f x) |> Some
    | None -> None