open Fake.Core
open Fake.IO.Globbing.Operators

open Dap.Build

module NuGet = Dap.Build.NuGet

let feed =
    NuGet.Feed.Create (
        apiKey = NuGet.Environment "API_KEY_nuget_org"
    )

let projects =
    !! "../src/Dap.Build/*.fsproj"


[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> Context.FakeExecutionContext.Create false "build.fsx"
    |> Context.RuntimeContext.Fake
    |> Context.setExecutionContext
    NuGet.createAndRun NuGet.release feed projects
    0