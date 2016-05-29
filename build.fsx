#r "packages/Build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.OpenCoverHelper
open Fake.ReleaseNotesHelper
open Fake.Git
open Fake.AssemblyInfoFile
open System
open System.IO

// ------------------------------------------------------------------------------------------
// Build parameters

let buildDirectory = "./build/"
let reportsDirectory = "./reports/"
let binDirectory = "./bin/"
let toolsDirectory = "./tools"

// Extract information from the pending release
let releaseNotes = parseReleaseNotes (File.ReadAllLines "RELEASE_NOTES.md")

// Automatically perform a full release from a master branch
let isRelease = getBranchName __SOURCE_DIRECTORY__ <> "master"

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

    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title (projectName)
          Attribute.Product "RethinkFSharp"
          Attribute.Description "A RethinkDB client driver with all the functional goodness of F#"
          Attribute.Version releaseNotes.AssemblyVersion
          Attribute.FileVersion releaseNotes.AssemblyVersion ]

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    sourceSets
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->
        CreateFSharpAssemblyInfo (folderName @@ "AssemblyInfo.fs") attributes)
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

    // Not long till Windows 10 supports Bash natively :)

    setEnvironVar "PATH" "C:\\cygwin64;C:\\cygwin64\\bin;C:\\cygwin;C:\\cygwin\\bin;%PATH%"

    let codeCovScript = (toolsDirectory @@ "CodeCov.sh")

    let exitCode = ExecProcess (fun info ->
        info.FileName <- "curl"
        info.Arguments <- "-s https://codecov.io/bash -o " + codeCovScript) (TimeSpan.FromMinutes 2.0)

    if exitCode <> 0 then
        failwithf "Could not download the bash uploader from CodeCov.io. Expecting cygwin and curl to be installed"

    let exitCode = ExecProcess (fun info -> 
        info.FileName <- "bash"
        info.Arguments <- (sprintf "%s -f %s" codeCovScript codeCoverageReport)) (TimeSpan.FromMinutes 5.0)

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

Target "GithubRelease" DoNothing

Target "All" DoNothing

"Clean"
    ==> "PatchAssemblyInfo"
    ==> "Build"
    ==> "BuildTests"
    ==> "RunUnitTests"
    ==> "PublishCodeCoverage"
    =?> ("NugetPackage", isRelease)
    =?> ("PublishNugetPackage", isRelease)
    =?> ("GithubRelease", isRelease)
    ==> "All"

RunTargetOrDefault "All"