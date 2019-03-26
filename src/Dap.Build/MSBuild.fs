[<RequireQualifiedAccess>]
module Dap.Build.MSBuild

open System
open System.IO
open System.Text.RegularExpressions
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.Core.TargetOperators

let restore proj =
    let setOptions = fun (options' : MSBuildParams) ->
        options'
    MSBuild.runDebug setOptions null "restore" [proj]
    |> List.iter traceProgress

let build (useDebugConfig : bool) proj =
    let setOptions = fun (options' : MSBuildParams) ->
        { options' with
            DoRestore = false
        }
    if useDebugConfig then
        MSBuild.runDebug setOptions null "" [proj]
    else
        MSBuild.runRelease setOptions null "" [proj]
    |> List.iter traceProgress

