`Dap.Build` is a simple library for simplify Fake target creation for DotNet projects.

## Current main features

- Create individual targets for multiple projects (clean/restore/build)
- Also create aggregated targets to do actions upon all projects
- For libraries, can generate nuget packages, also can push to either nuget.org or private progit server
- For applications, can also run certain project
- Inject generated nuget package into local nuget cache, for easier local development

## Sample `build.fsx`

```F#
#r "paket: groupref Build //"
#load ".fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.IO.Globbing.Operators

module NuGet = Dap.Build.NuGet

let feed =
    NuGet.Feed.Create (
        server = NuGet.ProGet "https://nuget.yjpark.org/nuget/dap",
        apiKey = NuGet.Environment "API_KEY_nuget_yjpark_org"
    )

let projects =
    !! "lib/Dap.FlatBuffers/*.csproj"
    ++ "src/Fable.Dap.Prelude/*.fsproj"
    ++ "src/Dap.Prelude/*.fsproj"
    ++ "src/Fable.Dap.Context/*.fsproj"
    ++ "src/Dap.Context/*.fsproj"
    ++ "src/Fable.Dap.Platform/*.fsproj"
    ++ "src/Dap.Platform/*.fsproj"
    ++ "src/Fable.Dap.WebSocket/*.fsproj"
    ++ "src/Dap.WebSocket/*.fsproj"
    ++ "src/Fable.Dap.Remote/*.fsproj"
    ++ "src/Dap.Remote/*.fsproj"
    ++ "src/Fable.Dap.Dsl/*.fsproj"
    ++ "src/Dap.Archive/*.fsproj"

NuGet.createAndRun NuGet.release feed project
```

## Links
- [Nuget Package] (https://www.nuget.org/packages/Dap.Build/)
- [Blog Post] (http://blog.yjpark.org/blog/2018/09/01/build-dotnet-projects-with-fake/)