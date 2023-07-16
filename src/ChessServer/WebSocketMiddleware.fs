
module ChessServer.Middleware

//open System.Threading
//open System.Threading.Tasks
//open Microsoft.AspNetCore.Http
//open Logging
//open Microsoft.Extensions.Logging
//open FSharp.Control.Tasks.V2

//type WebSocketMiddleware(next : RequestDelegate) =
//    let logger = getLoggerOfType<WebSocketMiddleware>()
//    let mutable activeConnections = 0

//    member __.Invoke(ctx : HttpContext) = task {
//        if ctx.Request.Path = PathString("/ws") then
//            match ctx.WebSockets.IsWebSocketRequest with
//            | true ->
//                let! webSocket = ctx.WebSockets.AcceptWebSocketAsync()
//                let connectionId = ctx.Connection.Id
//                let total = Interlocked.Increment(&activeConnections)
//                logger.LogInformation("Accept connection {con}, total: {total}", connectionId, total)
//                do! SocketManager.processConnection webSocket connectionId
//                //do! Task.Delay(20 * 1000)
//                let total = Interlocked.Decrement(&activeConnections)
//                logger.LogInformation("Close connection {con}, total: {total}", connectionId, total)
//            | false -> ctx.Response.StatusCode <- 400
//        else
//            next.Invoke(ctx) |> ignore
//    }

