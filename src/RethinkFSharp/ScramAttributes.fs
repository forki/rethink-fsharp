namespace RethinkFSharp.Network

/// All permissible attributes that form part of the SCRAM authentication process as
/// defined in RFC 5802, https://tools.ietf.org/html/rfc5802#section-5.1
type internal ScramAttributes = 
    {
        AuthorizationIdentity : string option
        Username : string option
        Nonce : string option
        HeaderAndChannelBinding : string option
        Salt : string option
        HashIterationCount : int option
        ClientProof : string option
        ServerSignature : string option
        ServerError : string option
    }
    static member Default =
        {
            AuthorizationIdentity = None
            Username = None
            Nonce = None
            HeaderAndChannelBinding = None
            Salt = None
            HashIterationCount = None
            ClientProof = None
            ServerSignature = None
            ServerError = None
        }

module internal Scram =
    let internal clientFirstMessage scramAttributes =
        ""

    let internal clientFinalMessage scramAttributes =
        ""