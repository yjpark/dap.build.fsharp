(* FAKE: 5.22.0 *)
#r "paket: groupref Main //"
#load ".fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.IO.Globbing.Operators

#load "src/Dap.Build/Util.fs"
#load "src/Dap.Build/MSBuild.fs"
#load "src/Dap.Build/DotNet.fs"
#load "src/Dap.Build/NuGet.fs"
module NuGet = Dap.Build.NuGet

let feed =
    NuGet.Feed.Create (
        apiKey = NuGet.Environment "API_KEY_nuget_org"
    )

let projects =
    !! "src/Dap.Build/*.fsproj"

NuGet.createAndRun NuGet.release feed projects
