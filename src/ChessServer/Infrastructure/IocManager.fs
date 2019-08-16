module IocManager

open System

let mutable private iocContainer : IServiceProvider = null
let setContainer container = iocContainer <- container
let getContainer() = 
    match iocContainer with
    | null -> failwith "Ioc container is not set"
    | x -> x