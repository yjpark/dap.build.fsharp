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

let getWorkingDir proj =
    (new FileInfo (proj)).Directory.FullName

let pack (useDebugConfig : bool) proj =
    let config = if useDebugConfig then "Debug" else "Release"
    let args = sprintf "pack -OutputDirectory bin/%s -Properties Configuration=%s" config config
    let result =
        CreateProcess.fromRawCommandLine "nuget" args
        |> CreateProcess.withWorkingDirectory (getWorkingDir proj)
        |> Proc.run
    if result.ExitCode <> 0 then
        traceFailure <| sprintf "nuget failed with exitcode '%d'" result.ExitCode
