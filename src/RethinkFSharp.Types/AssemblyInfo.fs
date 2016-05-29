namespace System
open System.Reflection
open System.Runtime.CompilerServices

[<assembly: AssemblyTitleAttribute("RethinkFSharp.Types")>]
[<assembly: AssemblyProductAttribute("RethinkFSharp")>]
[<assembly: AssemblyDescriptionAttribute("A RethinkDB client driver with all the functional goodness of F#")>]
[<assembly: AssemblyCompanyAttribute("Coda Solutions Ltd")>]
[<assembly: AssemblyVersionAttribute("0.1.0")>]
[<assembly: AssemblyFileVersionAttribute("0.1.0")>]
[<assembly: InternalsVisibleToAttribute("RethinkFSharpTests")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.1.0"
    let [<Literal>] InformationalVersion = "0.1.0"
