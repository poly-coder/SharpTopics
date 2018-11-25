#r "paket:
nuget Fake.IO.FileSystem
nuget Fake.DotNet.MSBuild
nuget Fake.Core.Target
//"
#load "./.fake/build.fsx/intellisense.fsx"

open Fake.Core
open Fake.IO
open Fake.IO.Globbing.Operators
open Fake.DotNet

// Properties
let buildDir = "./build/"

// Targets
Target.create "Clean" (fun _ ->
  Shell.cleanDir buildDir
)

Target.create "BuildFunky" (fun _ ->
  !! "SharpFunky*/**/*.fsproj"
    |> MSBuild.runRelease id buildDir "Build"
    |> Trace.logItems "BuildFunky-Output: "
)

Target.create "Default" (fun _ ->
  Trace.trace "Hello world from FAKE"
)

// Dependencies
open Fake.Core.TargetOperators

"Clean"
  ==> "BuildFunky"
  ==> "Default"

Target.runOrDefault "Default"
