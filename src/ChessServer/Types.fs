namespace ChessServer

module Types =


    type PingCommand = { Message:string }
    type PungCommand = { Message:string }
    type ChatCommand = { Message:string; Channel:string }
    //type MatchCommand = {}

    type PongCommand = { Message:string }
    type NotifyCommand = { Message:string }
    

    type InputCommand =
    | PingCommand of PingCommand
    | PungCommand of PungCommand
    | MatchCommand
    | ChatCommand of ChatCommand

    type OutputCommand =
    | PongCommand of PongCommand
    | NotifyCommand of NotifyCommand