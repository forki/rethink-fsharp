module ScramAttributesTests

open Xunit
open FsUnit.Xunit
open RethinkFSharp.Network

// This type is based on a SCRAM exchange as defined within RFC 5802, 
// https://tools.ietf.org/html/rfc5802#section-5, this is used strictly for
// message construction and does not represent a complete exchange.
let private scramAttributes = 
    {
        AuthorizationIdentity = None
        Username = Some("user")
        Nonce = Some("fyko+d2lbbFgONRv9qkxdawL")
        HeaderAndChannelBinding = Some("biws")
        Salt = Some("QSXCR+Q6sek8bf92")
        HashIterationCount = Some(4096)
        ClientProof = Some("v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=")
        ServerSignature = Some("rmF9pqV8S7suAoZWja4dJRkFsKQ=")
        ServerError = None
    }

[<Fact>]
let ``a client first message should always start with n,,``() =
    let firstMessage = Scram.clientFirstMessage scramAttributes
    
    firstMessage.Substring(0, 3) |> should equal "n,,"

[<Fact>]
let ``a client first message should contain a username, followed by a nonce``() =
    let firstMessage = Scram.clientFirstMessage scramAttributes

    let username = scramAttributes.Username.Value
    let nonce = scramAttributes.Nonce.Value

    let expectedFirstMessage = sprintf "u=%s,r=%s" username nonce

    firstMessage.Substring(0, 3) |> should equal expectedFirstMessage

[<Fact>]
let ``a client final message should contain in order, a channel binding, nonce and client proof``() =
    let finalMessage = Scram.clientFinalMessage scramAttributes

    let headerAndChannelBinding = scramAttributes.HeaderAndChannelBinding.Value
    let nonce = scramAttributes.Nonce.Value
    let clientProof = scramAttributes.ClientProof.Value

    let expectedFinalMessage = 
        sprintf "c=%s,r=%s,p=%s" headerAndChannelBinding nonce clientProof

    finalMessage |> should equal expectedFinalMessage