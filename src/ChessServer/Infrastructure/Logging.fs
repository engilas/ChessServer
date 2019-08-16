module Logging

open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging

let getLoggerOfType<'a>() = IocManager.getContainer().GetService<ILogger<'a>>()
let getLogger str = IocManager.getContainer().GetService<ILoggerFactory>().CreateLogger(str)