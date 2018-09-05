[<AutoOpen>]
module Dap.Build.Util

open Fake.Core

let traceProgress info =
    Trace.traceFAKE "    -> %s" info

let traceSuccess info =
    Trace.tracefn  "    -> %s" info

let traceFailure info =
    Trace.traceErrorfn  "    -> %s" info
