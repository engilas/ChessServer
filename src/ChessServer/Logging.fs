namespace ChessServer

module IocManager =
    open System

    let mutable private iocContainer : IServiceProvider = null
    let setContainer container = iocContainer <- container
    let getContainer() = 
        match iocContainer with
        | null -> failwith "Ioc container is not set"
        | x -> x

module Logging =
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.Extensions.Logging

    let getLoggerOfType<'a>() = IocManager.getContainer().GetService<ILogger<'a>>()
    let getLogger str = IocManager.getContainer().GetService<ILoggerFactory>().CreateLogger(str)



