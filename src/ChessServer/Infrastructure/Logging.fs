module Logging

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open System

[<AutoOpen>]
module private Internal =
    type EmptyLogger() = 
        interface ILogger with
            member this.BeginScope(state: 'TState): IDisposable = 
                raise (System.NotImplementedException())
            member this.IsEnabled(logLevel: LogLevel): bool = 
                true
            member this.Log(logLevel: LogLevel, eventId: EventId, state: 'TState, ``exception``: exn, formatter: Func<'TState,exn,string>): unit = 
                printf "[%s]: %s" <| logLevel.ToString() <| formatter.Invoke(state, ``exception``)

    type TypedEmptyLogger<'a>() = 
        inherit EmptyLogger()
        interface ILogger<'a>

    let chooseLogger e n =
        match IocManager.getContainer() with
        | Some x -> e x
        | None -> n()

let getLoggerOfType<'a>() = 
    chooseLogger 
        (fun c -> c.GetService<ILogger<'a>>()) 
        (fun () -> TypedEmptyLogger<'a>() :> ILogger<'a>)

let getLogger str =
    chooseLogger
        (fun c -> c.GetService<ILoggerFactory>().CreateLogger(str)) 
        (fun () -> EmptyLogger() :> ILogger)