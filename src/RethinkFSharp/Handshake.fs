namespace RethinkFSharp.Network

open System

module internal Handshake =
    let private littleEndian bytes =
        Array.Reverse bytes
        bytes

    let serverVersion (version:int) =
        littleEndian <| BitConverter.GetBytes version


