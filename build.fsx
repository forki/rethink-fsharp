#r "packages/Build/FAKE/tools/FakeLib.dll"
#r "packages/Build/System.Management.Automation/lib/net45/System.Management.Automation.dll"
open Fake
open Fake.OpenCoverHelper
open Fake.ReleaseNotesHelper
open System
open System.IO
open System.Management.Automation

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
    CleanDirs[buildDirectory; reportsDirectory; binDirectory; toolsDirectory]
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

    // Not long till Windows 10 supports Bash natively :)

    setEnvironVar "PATH" "C:\\cygwin64;C:\\cygwin64\\bin;%PATH%"

    let codeCovScript = (toolsDirectory @@ "CodeCov.sh")

    PowerShell.Create()
        .AddScript("(New-Object System.Net.WebClient).DownloadFile(\"https://codecov.io/bash\", \"" + codeCovScript + "\")")
        .Invoke() |> ignore

    let exitCode = ExecProcess (fun info -> 
        info.FileName <- "bash"
        info.Arguments <- (sprintf "%s -f %s -t %s" codeCovScript codeCoverageReport codeCovToken)) (TimeSpan.FromMinutes 5.0)

    if exitCode <> 0 then
        failwithf "Failed to upload the codecov coverage report"
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