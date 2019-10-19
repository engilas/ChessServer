module ChessServer.IocManager

open System

let mutable private iocContainer : IServiceProvider = null
let setContainer container = iocContainer <- container
let getContainer() = 
    match iocContainer with
    | null -> None
    | x -> Some x