#r "packages/build/FAKE/tools/FakeLib.dll"
#r "packages/build/DotNetZip/lib/net20/Ionic.Zip.dll"
#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
open System
open System.IO
open Fake
open Fake.OpenCoverHelper
open Fake.ReleaseNotesHelper
open Fake.Git
open Fake.AssemblyInfoFile
open Ionic.Zip
open Octokit

// ------------------------------------------------------------------------------------------
// Build parameters

let buildDirectory = "./build/"
let reportsDirectory = "./reports/"
let binDirectory = "./bin/"
let toolsDirectory = "./tools"
let keysDirectory = "./keys"

// Project files for building and testing
let sourceSets = !! "src/**/*.fsproj"
let testSets = !! "test/**/*.fsproj"

// Extract information from the pending release
let releaseNotes = parseReleaseNotes (File.ReadAllLines "RELEASE_NOTES.md")

// Automatically perform a full release from a master branch
let isRelease = getBranchName __SOURCE_DIRECTORY__ = "master"

// Not long till Windows 10 supports Bash natively :)

setEnvironVar "PATH" "C:\\cygwin64;C:\\cygwin64\\bin;C:\\cygwin;C:\\cygwin\\bin;%PATH%"

// ------------------------------------------------------------------------------------------
// Clean targets

Target "Clean" (fun _ -> 
    CleanDirs[buildDirectory; reportsDirectory; binDirectory; toolsDirectory]
)

// ------------------------------------------------------------------------------------------
// Strong name signing and patching

Target "DecryptSigningKey" (fun _ ->
    trace "Decrypt signing key for strong named assemblies..."

    Copy keysDirectory ["./packages/build/secure-file/tools/secure-file.exe"]

    let decryptKeyPath = currentDirectory @@ "keys" @@ "decrypt-key.cmd"

    let exitCode = ExecProcess (fun info -> 
        info.FileName <- decryptKeyPath
        info.WorkingDirectory <- keysDirectory) (TimeSpan.FromMinutes 1.0)

    if exitCode <> 0 then
        failwithf "Failed to decrypt the signing key"
)

Target "PatchAssemblyInfo" (fun _ ->
    trace "Patching all assemblies..."

    let publicKey = (File.ReadAllText "./keys/RethinkFSharp.pk")

    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title (projectName)
          Attribute.Product "RethinkFSharp"
          Attribute.Description "A RethinkDB client driver with all the functional goodness of F#"
          Attribute.Company "Coda Solutions Ltd"
          Attribute.Version releaseNotes.AssemblyVersion
          Attribute.FileVersion releaseNotes.AssemblyVersion
          Attribute.KeyFile "../../keys/RethinkFSharp.snk"
          Attribute.InternalsVisibleTo (sprintf "RethinkFSharpTests,PublicKey=%s" publicKey) ]

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

// ------------------------------------------------------------------------------------------
// Strong name third party dlls

Target "StrongNameTestDependencies" (fun _ ->
    trace "Strong name sign third party dlls..."

    // It is important we sign before test compilation. At this point, FSharp.Core will have been copied 
    // to the build directory already, so we use that as our working directory.

    let strongNameSignerPath = currentDirectory @@ "packages/build/Brutal.Dev.StrongNameSigner/tools/StrongNameSigner.Console.exe"
    let keyFilePath = currentDirectory @@ "keys" @@ "RethinkFSharp.snk"

    ["packages/test/FsUnit.Xunit/lib/net45/FsUnit.Xunit.dll"] 
    |> Seq.iter (fun dll ->
        let dllToSignPath = Path.GetFullPath(dll)
        
        trace dllToSignPath

        let exitCode = ExecProcess (fun info -> 
            info.FileName <- strongNameSignerPath
            info.Arguments <- (sprintf "-a \"%s\" -k \"%s\"" dllToSignPath keyFilePath)
            info.WorkingDirectory <- buildDirectory) (TimeSpan.FromMinutes 2.0)

        if exitCode <> 0 then
            failwithf "Failed to strong name third party dll '%s' with key" <| Path.GetFileName(dll)
    )
)

// ------------------------------------------------------------------------------------------
// Run Unit Tests and generate Code Coverage Report

let rethinkDbProcess = "rethinkdb.exe"

Target "StartRethinkDb" (fun _ ->
    let zipFile = toolsDirectory @@ "RethinkDb.zip"

    let exitCode = ExecProcess (fun info ->
        info.FileName <- "curl"
        info.Arguments <- "https://download.rethinkdb.com/windows/rethinkdb-2.3.3.zip -o " + zipFile) (TimeSpan.FromMinutes 5.0)

    if exitCode <> 0 then failwithf "Unable to download the latest version of RethinkDB for windows"

    let rethinkDbExe = toolsDirectory @@ rethinkDbProcess
    let rethinkDbDataPath = __SOURCE_DIRECTORY__ @@ "test" @@ "rethinkdb_data"

    use zip = new ZipFile(zipFile)
    zip.FlattenFoldersOnExtract <- true
    zip.ExtractAll toolsDirectory
    
    trace "Attempting to start the RethinkDB server..."
    fireAndForget(fun ps ->
        ps.FileName <- rethinkDbExe
        ps.Arguments <- "-d " + rethinkDbDataPath)
)

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

Target "ShutdownRethinkDb" (fun _ ->
    trace "Attempting to shutdown the RethinkDB server..."
    killProcess rethinkDbProcess
)

Target "PublishCodeCoverage" (fun _ ->
    trace "Publishing code coverage report to CodeCov..."

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

    let nugetApiToken = environVarOrFail "NUGET_TOKEN"

    Paket.Push (fun p ->
        { p with
            ApiKey = nugetApiToken
            WorkingDir = binDirectory
        })
)

Target "GithubRelease" DoNothing

Target "All" DoNothing

"Clean"
    ==> "DecryptSigningKey"
    ==> "PatchAssemblyInfo"
    ==> "Build"
    ==> "StrongNameTestDependencies"
    ==> "BuildTests"
    ==> "StartRethinkDb"
    ==> "RunUnitTests"
    ==> "ShutdownRethinkDb"
    ==> "PublishCodeCoverage"
    =?> ("NugetPackage", isRelease)
    =?> ("PublishNugetPackage", isRelease)
    =?> ("GithubRelease", isRelease)
    ==> "All"

RunTargetOrDefault "All"