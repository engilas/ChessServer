module Session
    
open Types.Channel
open Types.Domain
open Types.Command
open SessionTypes
open ChessEngine.Engine
open SessionAgent
open System

let createSessionWithFen fen whitePlayer blackPlayer =
    
    let engine = if String.IsNullOrWhiteSpace(fen) then Engine() else Engine(fen)

    let state = {
        Engine = engine
        WhitePlayer = whitePlayer
        BlackPlayer = blackPlayer
        Next = White
        Status = Active
    }
    
    let onEndGame result reason =
        let endGameNotify = EndGameNotify {
            Result = result
            Reason = reason
        }
        whitePlayer.ChangeState New
        blackPlayer.ChangeState New
        whitePlayer.PushNotification endGameNotify
        blackPlayer.PushNotification endGameNotify

    let agent = createSessionAgent state onEndGame
    let createMoveFun color command = 
        agent.PostAndReply(fun channel -> Regular ({Source=color; Command = command}, channel))

    let push channel msg = ChatNotify {Message = msg} |> channel.PushNotification

    let checkStatus x =
        let state = agent.PostAndReply(fun channel -> GetState channel)
        match state.Status with
        | Terminated -> sessionError SessionTerminated
        | _ -> x

    let chatFun color msg =
        match color with
        | White -> push blackPlayer msg
        | Black -> push whitePlayer msg

    let closeFun color =
        let notify channel = SessionCloseNotify >> channel.PushNotification 

        whitePlayer.ChangeState New |> ignore
        blackPlayer.ChangeState New |> ignore
        agent.Post Terminate

        match color with 
        | White -> notify blackPlayer
        | Black -> notify whitePlayer

    let createSession color = {
        CreateMove = checkStatus >> (createMoveFun color)
        ChatMessage = checkStatus >> chatFun color
        CloseSession = checkStatus >> closeFun color
    }
    createSession White, createSession Black

let createSession = createSessionWithFen null

