#r "paket: groupref Build //"
#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.IO.Globbing.Operators

#load "src/Dap.Build/Util.fs"
#load "src/Dap.Build/DotNet.fs"
#load "src/Dap.Build/NuGet.fs"
module NuGet = Dap.Build.NuGet

let feed : NuGet.Feed = {
    NuGet.Source = "https://www.nuget.org/api/v2"
    NuGet.ApiKey = NuGet.Environment "API_KEY_nuget_org"
}

let projects =
    !! "src/Dap.Build/*.fsproj"

NuGet.createAndRun NuGet.release feed projects
