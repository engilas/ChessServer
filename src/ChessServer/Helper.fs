namespace ChessServer

module Helper =
    let invalidArg argName value msg = invalidArg argName <| sprintf "'%A': %s" value msg