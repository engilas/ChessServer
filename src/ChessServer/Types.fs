namespace ChessServer

module Types =


    type PingCommand = { Message:string }
    type PungCommand = { Message:string }
    type PongCommand = { Message:string }
    type NotifyCommand = { Message:string }

    type InputCommand =
    | PingCommand of PingCommand
    | PungCommand of PungCommand

    type OutputCommand =
    | PongCommand of PongCommand
    | NotifyCommand of NotifyCommand