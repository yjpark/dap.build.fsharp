[<RequireQualifiedAccess>]
module Dap.Build.Fable

open System
open System.IO
open System.Text.RegularExpressions
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.Core.TargetOperators
module Yarn = Fake.JavaScript.Yarn

module DapDotNet = Dap.Build.DotNet

[<Literal>]
let Install = "Install"

[<Literal>]
let Serve = "Serve"

[<Literal>]
let Bundle = "Bundle"

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

let private setYarnParam (proj : string) (param : Yarn.YarnParams) =
        { param with
            WorkingDirectory = Path.GetDirectoryName(proj)
        }

let install (options : Options) proj =
    Trace.traceFAKE "Install Fable Project: %s" proj
    Yarn.install <| setYarnParam proj

let serve (options : Options) proj =
    Trace.traceFAKE "Watch Fable Project: %s" proj
    Yarn.exec "webpack-dev-server" <| setYarnParam proj

let bundle (options : Options) proj =
    Trace.traceFAKE "Bundle Fable Project: %s" proj
    Yarn.exec "webpack" <| setYarnParam proj

let private createTargets' (options : Options) noPrefix projects =
    let (label, prefix) = DapDotNet.getLabelAndPrefix noPrefix projects
    Target.setLastDescription <| sprintf "Serve %s" label
    Target.create (prefix + Install) (fun _ ->
        projects
        |> Seq.iter (install options)
    )
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
