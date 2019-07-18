namespace ChessServer

module Middleware =
    open System.Threading.Tasks
    open Microsoft.AspNetCore.Http
    open Logging
    open Microsoft.Extensions.Logging
    
    type WebSocketMiddleware(next : RequestDelegate) =
        let logger = getLoggerOfType<WebSocketMiddleware>()

        member __.Invoke(ctx : HttpContext) =
            async {
                if ctx.Request.Path = PathString("/ws") then
                    match ctx.WebSockets.IsWebSocketRequest with
                    | true ->
                        let! webSocket = ctx.WebSockets.AcceptWebSocketAsync() |> Async.AwaitTask
                        let connectionId = ctx.Connection.Id
                        logger.LogInformation("Accept connection {con}", connectionId)
                        do! SocketManager.processConnection webSocket connectionId
                        logger.LogInformation("Close connection {con}", connectionId)
                    | false -> ctx.Response.StatusCode <- 400
                else
                    next.Invoke(ctx) |> ignore
            } |> Async.StartAsTask :> Task

