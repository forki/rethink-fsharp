#r "packages/Build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.OpenCoverHelper
open Fake.ReleaseNotesHelper
open System
open System.IO

// ------------------------------------------------------------------------------------------
// Build parameters

let buildDirectory = "./build/"
let reportsDirectory = "./reports/"
let binDirectory = "./bin/"

// Detect if we are running this build on Appveyor
let isAppveyorBuild = environVar "APPVEYOR" <> null

// Extract information from the pending release
let releaseNotes = parseReleaseNotes (File.ReadAllLines "RELEASE_NOTES.md")

// ------------------------------------------------------------------------------------------
// Clean targets

Target "Clean" (fun _ -> 
    CleanDirs[buildDirectory; reportsDirectory; binDirectory]
)

// ------------------------------------------------------------------------------------------
// Build and Test targets

let sourceSets = !! "src/**/*.fsproj"

Target "PatchAssemblyInfo" (fun _ ->
    trace "Patching all assemblies..."
)

Target "Build" (fun _ ->
    MSBuildRelease buildDirectory "Rebuild" sourceSets
    |> Log "Build-Output: "
)

Target "BuildTests" (fun _ ->
    let testSets = !! "test/**/*.fsproj"

    MSBuildDebug buildDirectory "Build" testSets
    |> Log "BuildTests-Output: "
)

// ------------------------------------------------------------------------------------------
// Run Unit Tests and generate Code Coverage Report

let codeCoverageReport = (reportsDirectory @@ "code-coverage.xml")

Target "RunUnitTests" (fun _ ->
    trace "Executing tests and generating code coverage with OpenCover..."

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
    trace "Publishing code coverage report to CodeCov..."

    Shell.Exec(@"SET PATH=C:\Python34;C:\Python34\Scripts;%PATH%") |> ignore

    Shell.Exec("pip install codecov") |> ignore

    Shell.Exec("codecov -f " + codeCoverageReport) |> ignore
)

// ------------------------------------------------------------------------------------------
// Generate Nuget package and deploy

Target "NugetPackage" (fun _ ->
    trace "Building Nuget package with Paket..."

    Paket.Pack (fun p -> 
        { p with
            OutputPath = binDirectory
            Symbols = true
            Version = releaseNotes.NugetVersion
            ReleaseNotes = releaseNotes.Notes |> String.concat Environment.NewLine
        })
)

"Clean"
    ==> "PatchAssemblyInfo"
    ==> "Build"
    ==> "BuildTests"
    ==> "RunUnitTests"
    =?> ("PublishCodeCoverage", isAppveyorBuild)
    ==> "NugetPackage"

RunTargetOrDefault "NugetPackage"