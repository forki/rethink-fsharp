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
let toolsDirectory = "./tools"

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

    let codeCovToken = environVar "CODECOV_TOKEN"

    setEnvironVar "PATH" "C:\\Python34;C:\\Python34\\Scripts;%PATH%" |> ignore

    let exitCode = ExecProcess (fun info -> 
        info.FileName <- "pip" 
        info.Arguments <- "install --user codecov") (TimeSpan.FromMinutes 5.0)
    
    if exitCode <> 0 then failwithf "Could not download and install the codecov utility"

    let exitCode = ExecProcess (fun info -> 
        info.FileName <- "codecov") (TimeSpan.FromMinutes 5.0)

    if exitCode <> 0 then failwithf "Could not publish the code coverage report to codecov"
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

Target "PublishNugetPackage" (fun _ ->
    trace "Publishing Nuget package with Paket..."
)

Target "All" (fun _ ->
    ()
)

"Clean"
    ==> "PatchAssemblyInfo"
    ==> "Build"
    ==> "BuildTests"
    ==> "RunUnitTests"
    ==> "PublishCodeCoverage"
    ==> "NugetPackage"
    ==> "All"

RunTargetOrDefault "All"