[<RequireQualifiedAccess>]
module Dap.Build.Fable

open System
open System.IO
open System.Text.RegularExpressions
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.Core.TargetOperators

module DapDotNet = Dap.Build.DotNet

[<Literal>]
let Bundle = "Bundle"

[<Literal>]
let Serve = "Serve"

type Options = {
    DevConfig : string
    DevCleans : string seq
    ProdConfig : string
    ProdCleans : string seq
}

let options devCleans prodCleans = {
    DevConfig = "webpack.config.js"
    DevCleans = devCleans
    ProdConfig = "webpack.config.js"
    ProdCleans = prodCleans
}

let bundle (options : Options) proj =
    Trace.traceFAKE "Bundle Fable Project: %s" proj
    let setOptions = fun (options' : DotNet.Options) ->
        { options' with
            WorkingDirectory = Path.GetDirectoryName(proj)
        }
    let package = DapDotNet.getPackage proj
    sprintf "webpack -- -p --config %s" options.ProdConfig
    |> DotNet.exec setOptions "fable"
    |> fun result ->
        if not result.OK then
            failwith <| sprintf "Bundle Fable Project Failed: %s -> [%i] %A %A" package result.ExitCode result.Messages result.Errors

let serve (options : Options) proj =
    Trace.traceFAKE "Watch Fable Project: %s" proj
    let setOptions = fun (options' : DotNet.Options) ->
        { options' with
            WorkingDirectory = Path.GetDirectoryName(proj)
        }
    let package = DapDotNet.getPackage proj
    sprintf "webpack-dev-server --port free -- --config %s" options.DevConfig
    |> DotNet.exec setOptions "fable"
    |> fun result ->
        if not result.OK then
            failwith <| sprintf "Watch Fable Project Failed: %s -> [%i] %A %A" package result.ExitCode result.Messages result.Errors

let private createTargets' (options : Options) noPrefix projects =
    let (label, prefix) = DapDotNet.getLabelAndPrefix noPrefix projects
    Target.setLastDescription <| sprintf "Serve %s" label
    Target.create (prefix + Serve) (fun _ ->
        File.deleteAll options.DevCleans
        projects
        |> Seq.iter (serve options)
    )
    Target.setLastDescription <| sprintf "Bundle %s" label
    Target.create (prefix + Bundle) (fun _ ->
        File.deleteAll options.ProdCleans
        projects
        |> Seq.iter (bundle options)
    )
    prefix + DapDotNet.Build
        ==> prefix + Serve
    |> ignore
    prefix + DapDotNet.Build
        ==> prefix + Bundle
    |> ignore

let createPerProjectTarget options proj =
    createTargets' options false [proj]

let create (options : Options) projects =
    projects
    |> Seq.iter (createPerProjectTarget options)
