#r "packages/Build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.OpenCoverHelper
open Fake.Testing.XUnit2

// ------------------------------------------------------------------------------------------
// Build parameters

let buildDirectory = "./build/"
let reportsDirectory = "./reports/"

let sourceSets = !! "src/**/*.fsproj"
let testSets = !! "test/**/*.fsproj"

let isAppveyorBuild = environVar "APPVEYOR" <> null

let codeCoverageReport = (reportsDirectory @@ "code-coverage.xml")

// ------------------------------------------------------------------------------------------
// Clean targets

Target "Clean" (fun _ -> 
    CleanDirs[buildDirectory; reportsDirectory]
)

// ------------------------------------------------------------------------------------------
// Build and Test targets

Target "Build" (fun _ ->
    MSBuildRelease buildDirectory "Rebuild" sourceSets
    |> Log "Build-Output: "
)

Target "BuildTests" (fun _ ->
    MSBuildDebug buildDirectory "Build" testSets
    |> Log "BuildTests-Output: "
)

Target "RunUnitTests" (fun _ ->
    trace "Executing tests and generating code coverage with OpenCover"

    let assembliesToTest = 
        !! (buildDirectory @@ "*Tests.dll") 
        |> Seq.toArray 
        |> String.concat " "

    OpenCover (fun p ->
        { p with
            ExePath = "./packages/build/OpenCover/tools/OpenCover.Console.exe"
            WorkingDir = __SOURCE_DIRECTORY__
            TestRunnerExePath = "./packages/build/xunit.runner.console/tools/xunit.console.exe"
            Output = codeCoverageReport
            Register = RegisterType.RegisterUser
            Filter = "+[RethinkFSharp*]* -[*.Tests*]*"
        })
        (assembliesToTest + " -appveyor -noshadow")
)

Target "PublishCodeCoverage" (fun _ ->
    trace "Publishing code coverage report to CodeCov"

    Shell.Exec(@"SET PATH=C:\Python34;C:\Python34\Scripts;%PATH%") |> ignore

    Shell.Exec("pip install codecov") |> ignore

    Shell.Exec("codecov -f " + codeCoverageReport) |> ignore
)

"Clean"
    ==> "Build"
    ==> "BuildTests"
    ==> "RunUnitTests"

"RunUnitTests"
    =?> ("PublishCodeCoverage", isAppveyorBuild)

RunTargetOrDefault "RunUnitTests"