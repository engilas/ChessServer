module TestHelper

open Xunit
open System

let invalidArgument = typeof<ArgumentException>
let nullArgument = typeof<ArgumentNullException>
