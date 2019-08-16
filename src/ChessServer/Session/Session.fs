module Session
    
open Types.Channel
open Types.Domain
open Types.Command
open SessionTypes
open ChessEngine.Engine
open SessionAgent

let createSession whitePlayer blackPlayer =
    let engine = Engine()

    let state = {
        Engine = engine
        WhitePlayer = whitePlayer
        BlackPlayer = blackPlayer
        Next = White
        Status = Active
    }

    let agent = sessionAgent state
    let createMoveFun color command = 
        agent.PostAndAsyncReply(fun a -> Regular ({Source=color; Command = command}, a))

    let push channel msg = ChatNotify {Message = msg} |> channel.PushNotification

    let chatFun color msg =
        match color with
        | White -> push blackPlayer msg
        | Black -> push whitePlayer msg

    let closeFun color msg =
        let closeInternal channel = 
            channel.ChangeState New |> ignore
            SessionCloseNotify {Message = sprintf "Session closed with reason: %s" msg} |> channel.PushNotification

        match color with 
        | White -> closeInternal blackPlayer
        | Black -> closeInternal whitePlayer

    let whiteSession = {
        CreateMove = createMoveFun White
        ChatMessage = chatFun White
        CloseSession = closeFun White
    }

    let blackSession = {
        CreateMove = createMoveFun Black
        ChatMessage = chatFun Black
        CloseSession = closeFun Black
    }

    whiteSession, blackSession



