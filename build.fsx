#r "packages/Build/FAKE/tools/FakeLib.dll"
open Fake
open Fake.Testing.XUnit2

// ------------------------------------------------------------------------------------------
// Build parameters

let buildDirectory = "./build/"
let reportsDirectory = "./reports/"

let sourceSets = !! "src/**/*.fsproj"
let testSets = !! "test/**/*.fsproj"

// ------------------------------------------------------------------------------------------
// Clean targets

Target "Clean" (fun _ -> 
    CleanDirs[buildDirectory]
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

Target "UnitTests" (fun _ ->
    !! (buildDirectory @@ "*Tests.dll")
    |> xUnit2 (fun p -> 
        { p with
            ShadowCopy = true;
            HtmlOutputPath = Some (reportsDirectory @@ "test-results.html")
        })
)

"Clean"
    ==> "Build"
    ==> "BuildTests"
    ==> "UnitTests"

RunTargetOrDefault "UnitTests"