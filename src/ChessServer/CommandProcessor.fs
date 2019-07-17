namespace ChessServer

module CommandProcessor =
    open Types

    let processCommand cmd pushCommand pushNotify =
        match cmd with
        | PingCommand ping -> 
            let pong = PongCommand {Message=ping.Message}
            pushCommand pong
            ()
        ()

