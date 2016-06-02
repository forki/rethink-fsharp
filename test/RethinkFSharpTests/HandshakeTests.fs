module HandshakeTests

open System
open Xunit
open FsUnit.Xunit
open RethinkFSharp.Network

[<Fact>]
let ``the server version is converted to a little endian integer``() =
    let versionBytes = Handshake.serverVersion 0x34c2bdc3
    let expectedBytes = BitConverter.GetBytes 0xc3bdc234

    versionBytes |> should equal expectedBytes

(*
The following exchange is being tested against a known vector defined within RFC5802, 
https://tools.ietf.org/html/rfc5802#section-5

C: n,,n=user,r=fyko+d2lbbFgONRv9qkxdawL
S: r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096
C: c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=
S: v=rmF9pqV8S7suAoZWja4dJRkFsKQ=
*)

