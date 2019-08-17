
module FsUnit.Xunit

open System
open NHamcrest.Core
open System.Reflection
open System.Runtime.ExceptionServices

let private call (f: obj) =
    match f with
    | :? (unit -> unit) as testFunc -> testFunc()
    | _ -> 
        let method = f.GetType().GetMethod("Invoke")
        try
            method.Invoke(f, [| () :> obj |]) |> ignore
        with :? TargetInvocationException as e ->
            ExceptionDispatchInfo.Capture(e.InnerException).Throw()
        ()
    false

let throwWithMessage (m:string) (t:Type) = 
    CustomMatcher<obj>(
        sprintf "%s \"%s\"" (string t) m,
        fun f -> 
            try call f
            with ex -> if ex.GetType() = t && ex.Message.Contains(m, StringComparison.OrdinalIgnoreCase) then true else false )

let throw (t:Type) = 
    CustomMatcher<obj>(
        string t,
        fun f -> 
            try call f
            with ex -> if ex.GetType() = t then true else false )