[<RequireQualifiedAccess>]
module Dap.Build.NuGet

open System
open System.IO
open System.Text.RegularExpressions
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.Net
open Fake.Core.TargetOperators

module DapDotNet = Dap.Build.DotNet

[<Literal>]
let Fetch = "Fetch"

[<Literal>]
let Pack = "Pack"

[<Literal>]
let Push = "Push"

[<Literal>]
let Develop = "Develop"

[<Literal>]
let Inject = "Inject"

[<Literal>]
let Recover = "Recover"

type ApiKey =
    | Environment of string
    | Plain of string
    | NoAuth

type NugetServer =
    | NugetOrg
    | ProGet of string

type Feed = {
    Server : NugetServer
    ApiKey : ApiKey
} with
    static member Create (apiKey : ApiKey, ?server : NugetServer) =
        let server = server |> Option.defaultValue NugetOrg
        {
            Server = server
            ApiKey = apiKey
        }
    member this.FetchUrl =
        match this.Server with
        | NugetOrg ->
            "https://www.nuget.org/api/v2/package"
        | ProGet url ->
            sprintf "%s/package" url
    member this.PushSource =
        match this.Server with
        | NugetOrg ->
            "https://www.nuget.org/api/v2/package"
        | ProGet url ->
            url

type Options = {
    DotNet : DapDotNet.Options
    CreateInjectTargets : bool
}

let debug = {
    DotNet = DapDotNet.debug
    CreateInjectTargets = true
}

let release = {
    DotNet = DapDotNet.release
    CreateInjectTargets = true
}

let checkVersion proj (releaseNotes : ReleaseNotes.ReleaseNotes) =
    let versionRegex = Regex("<Version>(.*?)</Version>", RegexOptions.IgnoreCase)
    File.ReadLines(proj)
    |> Seq.tryPick (fun line ->
        let m = versionRegex.Match(line)
        if m.Success then Some m else None)
    |> function
        | None -> failwith "Couldn't find version in project file"
        | Some m ->
            let version = m.Groups.[1].Value
            if version <> releaseNotes.NugetVersion then
                failwith <| sprintf "Mismatched version: project file => %s, RELEASE_NOTES.md => %s " version releaseNotes.NugetVersion
    releaseNotes

let loadReleaseNotes proj =
    let dir = Path.GetDirectoryName(proj)
    dir </> "RELEASE_NOTES.md"
    |> ReleaseNotes.load
    |> checkVersion proj

let private getApiKeyParam (apiKey : ApiKey) =
    match apiKey with
    | Environment key ->
        match Environment.environVarOrNone key with
        | Some key ->
            sprintf " -k %s" key
        | None ->
            failwith <| sprintf "Failed to get Api Key form environment: %s" key
    | Plain key ->
        sprintf " -k %s" key
    | NoAuth -> ""

let pack (options : Options) proj =
    Trace.traceFAKE "Pack NuGet Project: %s" proj
    let setOptions = fun (options' : DotNet.PackOptions) ->
        let releaseNotes = loadReleaseNotes proj
        let pkgReleaseNotes = sprintf "/p:PackageReleaseNotes=\"%s\"" (String.toLines releaseNotes.Notes)
        { options' with
            Configuration = options.DotNet.Configuration
            NoBuild = true
            Common =
                { options'.Common with
                    CustomParams = Some pkgReleaseNotes
                    DotNetCliPath = "dotnet"
                }
        }
    DotNet.pack setOptions proj

let homePath =
    match Environment.OSVersion.Platform with
    | PlatformID.Unix | PlatformID.MacOSX -> Environment.GetEnvironmentVariable("HOME")
    | _ -> Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");

let getNugetCachePath (package : string) (version : string option) =
    let path = Path.Combine [| homePath ; ".nuget" ; "packages" ; (package.ToLower ()) |]
    version
    |> Option.map (fun version ->
        Path.Combine [| path ; version |]
    )|> Option.defaultValue path

let getOriginalNugetCachePath (package : string) (version : string) =
    getNugetCachePath package <| Some (version + "-original")

let getSha512Stream (stream:Stream) =
    use hasher = System.Security.Cryptography.SHA512.Create() :> System.Security.Cryptography.HashAlgorithm
    Convert.ToBase64String(hasher.ComputeHash(stream))

let getSha512Value (filePath:string) =
    use stream = File.OpenRead(filePath)
    getSha512Stream stream

let extractNupkg path nupkgPath =
    let hash = getSha512Value nupkgPath
    let hashPath = nupkgPath + ".sha512"
    File.writeNew hashPath [hash]
    Zip.unzip path nupkgPath
    traceProgress nupkgPath
    traceProgress hash
    hash

let getCurrentHash nupkgPath =
    let hashPath = nupkgPath + ".sha512"
    if File.exists hashPath then
        let content = File.ReadAllLines hashPath
        if content.Length > 0 then
            content |> Array.head
        else ""
    else
        "Not Exist"

let doInject (package : string) (version : string) (pkg : string) =
    let path = getNugetCachePath package <| Some version
    Directory.ensure path
    let nupkgName = Path.GetFileName (pkg)
    let nupkgName = nupkgName.ToLower ()
    let nupkgPath = Path.Combine [| path ; nupkgName |]
    let injectPath = Path.Combine [| path ; "dap.build_inject.txt" |]
    if not (File.exists injectPath) then
        let originalPath = getOriginalNugetCachePath package version
        Directory.ensure originalPath
        Shell.cleanDir originalPath
        Shell.copyDir originalPath path (fun _ -> true)
    let oldHash = getCurrentHash nupkgPath
    Shell.cleanDir path
    Shell.copyFile nupkgPath pkg
    let hash = extractNupkg path nupkgPath
    if oldHash <> hash then
        traceSuccess "Injected As New Version"
    else
        traceProgress "Not Changed"
    File.writeNew injectPath [
        sprintf "Injected At: %A" System.DateTime.Now
        sprintf "SHA512 Hash: %s" hash
        sprintf "Previous Hash: %s" oldHash
        pkg
    ]

let inject (options : Options) proj =
    Trace.traceFAKE "Inject NuGet Project: %s" proj
    let dir = Path.GetDirectoryName(proj)
    let package = Path.GetFileName(dir)
    let releaseNotes = loadReleaseNotes proj
    let folder = DapDotNet.getConfigFolder options.DotNet.Configuration
    Directory.GetFiles(dir </> "bin" </> folder, "*.nupkg")
    |> Array.find (fun pkg -> pkg.Contains(releaseNotes.NugetVersion))
    |> doInject package releaseNotes.NugetVersion

let doRecover (package : string) (version : string) =
    let path = getNugetCachePath package <| Some version
    let originalPath = getOriginalNugetCachePath package version
    if DirectoryInfo.exists (DirectoryInfo.ofPath originalPath) then
        Shell.cleanDir path
        Shell.copyDir path originalPath (fun _ -> true)
        traceProgress path

let recover proj =
    Trace.traceFAKE "Recover NuGet Project: %s" proj
    let dir = Path.GetDirectoryName(proj)
    let package = Path.GetFileName(dir)
    let releaseNotes = loadReleaseNotes proj
    doRecover package releaseNotes.NugetVersion

let doFetch (feed : Feed) (package : string) (version : string) =
    let path = getNugetCachePath package <| Some version
    let nupkgName = sprintf "%s.%s.nupkg" package version
    let nupkgName = nupkgName.ToLower ()
    let nupkgPath = Path.Combine [| path ; nupkgName |]
    let url = sprintf "%s/%s/%s" feed.FetchUrl package version
    let oldHash = getCurrentHash nupkgPath
    Shell.cleanDir path
    Http.downloadFile nupkgPath url
    |> ignore
    let hash = extractNupkg path nupkgPath
    let fetchPath = Path.Combine [| path ; "dap.build_fetch.txt" |]
    File.writeNew fetchPath [
        sprintf "Download At: %A" System.DateTime.Now
        sprintf "Download From: %s" url
        sprintf "SHA512 Hash: %s" hash
        sprintf "Previous Hash: %s" oldHash
    ]
    if oldHash <> hash then
        traceSuccess "Updated To New Version"
    else
        traceProgress "Not Changed"
    let originalPath = getOriginalNugetCachePath package version
    if DirectoryInfo.exists (DirectoryInfo.ofPath originalPath) then
        Shell.deleteDir originalPath

let fetch (feed : Feed) proj =
    Trace.traceFAKE "Fatch NuGet Project: %s" proj
    let dir = Path.GetDirectoryName(proj)
    let package = Path.GetFileName(dir)
    let releaseNotes = loadReleaseNotes proj
    doFetch feed package releaseNotes.NugetVersion

let push (feed : Feed) proj =
    Trace.traceFAKE "Push NuGet Project: %s" proj
    let dir = Path.GetDirectoryName(proj)
    let releaseNotes = loadReleaseNotes proj
    let mutable pkgPath = ""
    Directory.GetFiles(dir </> "bin" </> "Release", "*.nupkg")
    |> Array.find (fun pkg -> pkg.Contains(releaseNotes.NugetVersion))
    |> (fun pkg ->
        pkgPath <- pkg
        sprintf "push %s -s %s%s" pkg feed.PushSource <| getApiKeyParam feed.ApiKey
    )|> DotNet.exec id "nuget"
    |> fun result ->
        if not result.OK then
            failwith <| sprintf "Push nupkg Failed: %s -> [%i] %A %A" pkgPath result.ExitCode result.Messages result.Errors

let createTargets' (extendOnly : bool) (options : Options) noPrefix feed projects =
    let (label, prefix) =
        if extendOnly then
            DapDotNet.getLabelAndPrefix noPrefix projects
        else
            DapDotNet.createTargets' options.DotNet noPrefix projects
    Target.setLastDescription <| sprintf "Fetch %s" label
    Target.create (prefix + Fetch) (fun _ ->
        projects
        |> Seq.iter (fetch feed)
    )
    Target.setLastDescription <| sprintf "Pack %s" label
    Target.create (prefix + Pack) (fun _ ->
        projects
        |> Seq.iter (pack options)
    )
    Target.setLastDescription <| sprintf "Push %s" label
    Target.create (prefix + Push) (fun _ ->
        projects
        |> Seq.iter (push feed)
    )
    prefix + DapDotNet.Build
        ==> prefix + Pack
        ==> prefix + Push
    |> ignore
    if options.CreateInjectTargets then
        Target.setLastDescription <| sprintf "Inject %s" label
        Target.create (prefix + Inject) (fun _ ->
            projects
            |> Seq.iter (inject options)
        )
        Target.setLastDescription <| sprintf "Recover %s" label
        Target.create (prefix + Recover) (fun _ ->
            projects
            |> Seq.iter recover
        )
        prefix + Pack
            ==> prefix + Inject
        |> ignore

let createTargets options =
    createTargets' false options true

let createPerProjectTarget options feed proj =
    createTargets' false options false feed [proj]

let create (options : Options) feed projects =
    createTargets options feed projects
    if options.DotNet.CreatePerProjectTargets then
        projects
        |> Seq.iter (createPerProjectTarget options feed)

let createAndRun (options : Options) feed projects =
    create options feed projects
    Target.runOrDefault Pack

let extendTargets options =
    createTargets' true options true

let extendPerProjectTarget options feed proj =
    createTargets' true options false feed [proj]

let extend (options : Options) feed projects =
    extendTargets options feed projects
    if options.DotNet.CreatePerProjectTargets then
        projects
        |> Seq.iter (extendPerProjectTarget options feed)

