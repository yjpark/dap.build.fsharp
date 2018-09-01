[<AutoOpen>]
module Dap.Build.Util

open Fake.Core

let trace info =
    Trace.traceFAKE "    -> %s" info
