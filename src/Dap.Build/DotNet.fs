[<RequireQualifiedAccess>]
module Dap.Build.DotNet

open System
open System.IO
open System.Text.RegularExpressions
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.Core.TargetOperators

[<Literal>]
let Clean = "Clean"

[<Literal>]
let Restore = "Restore"

[<Literal>]
let Prepare = "Prepare"

[<Literal>]
let Build = "Build"

[<Literal>]
let Run = "Run"

[<Literal>]
let WatchRun = "WatchRun"

[<Literal>]
let Publish = "Publish"

type IOptions =
    abstract CreatePerProjectTargets : bool with get
    abstract GetConfiguration : string -> DotNet.BuildConfiguration

type Options = {
    UseDebugConfig : bool
    CreatePerProjectTargets : bool
} with
    interface IOptions with
        member this.CreatePerProjectTargets = this.CreatePerProjectTargets
        member this.GetConfiguration _proj =
            if this.UseDebugConfig then DotNet.Debug else DotNet.Release

let debug : Options = {
    UseDebugConfig = true
    CreatePerProjectTargets = true
}

let release : Options = {
    UseDebugConfig = false
    CreatePerProjectTargets = true
}

[<NoComparison>]
type MixedOptions = {
    CreatePerProjectTargets : bool
    ReleasingProjects : string seq
} with
    interface IOptions with
        member this.CreatePerProjectTargets = this.CreatePerProjectTargets
        member this.GetConfiguration proj =
            let releasing = Seq.contains proj this.ReleasingProjects
            if releasing then DotNet.Release else DotNet.Debug

let mixed (releasingProjects)  : MixedOptions = {
    CreatePerProjectTargets = true
    ReleasingProjects = releasingProjects
}

let getConfigFolder (config : DotNet.BuildConfiguration) =
    match config with
    | DotNet.Debug -> "Debug"
    | DotNet.Release -> "Release"
    | DotNet.Custom config -> config

let getPackage (proj : string) =
    let dir = Path.GetDirectoryName(proj)
    Path.GetFileName(dir)

let useMSBuild proj =
    let xamarinRegex = Regex("Xamarin\\.(iOS|Android|Mac)\\.(.*?)\\.targets")
    File.ReadLines(proj)
    |> Seq.tryPick (fun line ->
        let m = xamarinRegex.Match(line)
        if m.Success then Some m else None)
    |> function
        | None -> false
        | Some m ->
            traceSuccess <| sprintf "Use MSBuild: %A" m
            true

let isRunnable proj =
    let versionRegex = Regex("<OutputType>(.*?)</OutputType>", RegexOptions.IgnoreCase)
    File.ReadLines(proj)
    |> Seq.tryPick (fun line ->
        let m = versionRegex.Match(line)
        if m.Success then Some m else None)
    |> function
        | None -> false
        | Some m ->
            let v = m.Groups.[1].Value
            v.ToLower () = "exe"

let clean (_options : IOptions) proj =
    Trace.traceFAKE "Clean Project: %s" proj
    let dir = Path.GetDirectoryName(proj)
    Shell.cleanDirs [
        Path.Combine [| dir ; "bin" |]
        Path.Combine [| dir ; "obj" |]
    ]

let restore (_options : IOptions) (noDependencies : bool) proj =
    Trace.traceFAKE "Restore Project: %s" proj
    if useMSBuild proj then
        Dap.Build.MSBuild.restore proj
    else
        let setOptions = fun (options' : DotNet.RestoreOptions) ->
            if noDependencies then
                { options' with
                    Common =
                        { options'.Common with
                            CustomParams = Some "--no-dependencies"
                        }
                }
            else
                options'
        DotNet.restore setOptions proj

let build (options : IOptions) (noDependencies : bool) proj =
    Trace.traceFAKE "Build Project: %s" proj
    if useMSBuild proj then
        let configuration = options.GetConfiguration proj
        let useDebugConfig = (configuration = DotNet.BuildConfiguration.Debug)
        Dap.Build.MSBuild.build useDebugConfig proj
    else
        let setOptions = fun (options' : DotNet.BuildOptions) ->
            let mutable param = "--no-restore"
            if noDependencies then
                param <- sprintf "%s --no-dependencies" param
            { options' with
                Configuration = options.GetConfiguration proj
                Common =
                    { options'.Common with
                        CustomParams = Some param
                    }
            }
        DotNet.build setOptions proj

let private run' (cmd : string) (options : IOptions) proj =
    Trace.traceFAKE "Run Project: %s" proj
    let setOptions = fun (options' : DotNet.Options) ->
        { options' with
            WorkingDirectory = Path.GetDirectoryName(proj)
        }
    let package = getPackage proj
    let key = "RunArgs_" + package.Replace(".", "_")
    match Environment.environVarOrNone key with
    | Some v ->
        v
    | None ->
        Trace.traceFAKE "    Pass Args by Set Environment: %s" key
        ""
    |> sprintf "--no-build --configuration %s -- %s" (getConfigFolder (options.GetConfiguration proj))
    |> DotNet.exec setOptions cmd
    |> fun result ->
        if not result.OK then
            failwith <| sprintf "Run Project Failed: %s -> [%i] %A %A" package result.ExitCode result.Messages result.Errors

let run (options : IOptions) proj =
    run' "run" options proj

let watchRun (options : IOptions) proj =
    run' "watch run" options proj

let publish (options : IOptions) proj =
    Trace.traceFAKE "Publish Project: %s" proj
    let setOptions = fun (options' : DotNet.Options) ->
        { options' with
            WorkingDirectory = Path.GetDirectoryName(proj)
        }
    let package = getPackage proj
    sprintf "--no-build --configuration %s" (getConfigFolder (options.GetConfiguration proj))
    |> DotNet.exec setOptions "publish"
    |> fun result ->
        if not result.OK then
            failwith <| sprintf "Publish Project Failed: %s -> [%i] %A %A" package result.ExitCode result.Messages result.Errors

let getLabelAndPrefix (noPrefix : bool) (projects : seq<string>) =
    let len = Seq.length projects
    if len = 0 then
        failwith "Projects Is Empty"
    elif noPrefix then
        if len = 1 then
            (sprintf "%i Project" len, "")
        else
            (sprintf "%i Projects" len, "")
    else
        let label = getPackage(Seq.head projects)
        let prefix = if noPrefix then "" else label + ":"
        (label, prefix)

let createTargets' (options : IOptions) (noPrefix : bool) (projects : seq<string>) =
    let (label, prefix) = getLabelAndPrefix noPrefix projects
    Target.setLastDescription <| sprintf "Clean %s" label
    Target.create (prefix + Clean) (fun _ ->
        projects
        |> Seq.iter (clean options)
    )
    Target.setLastDescription <| sprintf "Restore %s" label
    Target.create (prefix + Restore) (fun _ ->
        projects
        |> Seq.iteri (fun i proj ->
            restore options (i > 0) proj
        )
    )
    Target.setLastDescription <| sprintf "Build %s" label
    Target.create (prefix + Build) (fun _ ->
        projects
        |> Seq.iteri (fun i proj ->
            build options (i > 0) proj
        )
    )
    prefix + Clean
        ==> prefix + Restore
        ==> prefix + Build
    |> ignore
    if Seq.length projects = 1 && isRunnable (Seq.head projects) then
        Target.setLastDescription <| sprintf "Run %s" label
        Target.create (prefix + Run) (fun _ ->
            projects
            |> Seq.iter (run options)
        )
        Target.setLastDescription <| sprintf "Watch Run %s" label
        Target.create (prefix + WatchRun) (fun _ ->
            projects
            |> Seq.iter (watchRun options)
        )
        Target.setLastDescription <| sprintf "Publish %s" label
        Target.create (prefix + Publish) (fun _ ->
            projects
            |> Seq.iter (publish options)
        )
        prefix + Build
            ==> prefix + Run
        |> ignore
        prefix + Build
            ==> prefix + WatchRun
        |> ignore
        prefix + Build
            ==> prefix + Publish
        |> ignore
    (label, prefix)

let createTargets options projects =
    createTargets' options true projects
    |> ignore

let createPerProjectTarget options proj =
    createTargets' options false [proj]
    |> ignore

let create (options : IOptions) projects =
    createTargets options projects
    if options.CreatePerProjectTargets then
        projects
        |> Seq.iter (createPerProjectTarget options)

let createAndRun (options : IOptions) projects =
    create options projects
    Target.runOrDefault Build

let createPrepare' (noPrefix : bool) (action : unit -> unit) (projects : string list) =
    let (label, prefix) =
        if noPrefix then
            (sprintf "%i Projects" projects.Length, "")
        else
            let project = projects |> List.head
            (project, project + ":")
    Target.setLastDescription <| sprintf "Prepare %s" label
    Target.create (prefix + Prepare) (fun _ ->
        action ()
    )
    prefix + Prepare
        ==> prefix + Build
    |> ignore
    (label, prefix)

let createPrepareTarget (prepares : (string list * (unit -> unit)) list) =
    let projects =
        prepares
        |> List.map fst
        |> List.concat
    let action = fun () ->
        prepares
        |> List.map snd
        |> List.iter (fun action -> action ())
    createPrepare' true action projects
    |> ignore

let createOnePrepareTarget ((projects, action) : string list * (unit -> unit)) =
    projects
    |> List.iter (fun project ->
        createPrepare' false action [project]
        |> ignore
    )

let createPrepares (prepares : (string list * (unit -> unit)) list) =
    createPrepareTarget prepares
    prepares
    |> List.iter createOnePrepareTarget
