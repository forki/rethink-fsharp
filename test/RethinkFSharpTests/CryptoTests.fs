module CryptoTests

open Xunit
open FsUnit.Xunit
open RethinkFSharp.Network
open System.Text

(*
The following test vectors are for the PBKDF2 SHA256 algorithm, and were taken from the following github page:

https://github.com/ircmaxell/PHP-PasswordLib/blob/master/test/Data/Vectors/pbkdf2-draft-josefsson-sha256.test-vectors

These vectors are based on the official IETF vectors (https://www.ietf.org/rfc/rfc6070.txt), but have been modified
for SHA256 instead of SHA1. A reduced subset of the vectors have been used here, as they are slow running tests.

Set 1
    P=password
    S=salt
    c=1
    dkLen=32
    DK=120fb6cffcf8b32c43e7225256c4f837a86548c92ccc35480805987cb70be17b

Set 2
    P=password
    S=salt
    c=2
    dkLen=32
    DK=ae4d0c95af6b46d32d0adff928f06dd02a303f8ef3c251dfd6e2d85a95474c43

Set 3
    P=password
    S=salt
    c=4096
    dkLen=32
    DK=c5e478d59288c841aa530db6845c4c8d962893a001ce4e11a4963873aa98134a

Set 4
    P=passwordPASSWORDpassword
    S=saltSALTsaltSALTsaltSALTsaltSALTsalt
    c=4096
    dkLen=40
    DK=348c89dbcbd32b2f32d814b8116e84cf2b17347ebc1800181c4e2a1fb8dd53e1c635518c7dac47e9

Set 5
    P=pass\0word
    S=sa\0lt
    c=4096
    dkLen=16
    DK=89b69d0516f829893c696226650a8687
*)

[<Theory>]
[<InlineData("password", "salt", 1, 32, "120fb6cffcf8b32c43e7225256c4f837a86548c92ccc35480805987cb70be17b")>]
[<InlineData("password", "salt", 2, 32, "ae4d0c95af6b46d32d0adff928f06dd02a303f8ef3c251dfd6e2d85a95474c43")>]
[<InlineData("password", "salt", 4096, 32, "c5e478d59288c841aa530db6845c4c8d962893a001ce4e11a4963873aa98134a")>]
[<InlineData("pass\000word", "sa\000lt", 4096, 16, "89b69d0516f829893c696226650a8687")>]
[<InlineData("passwordPASSWORDpassword", "saltSALTsaltSALTsaltSALTsaltSALTsalt", 4096, 40, "348c89dbcbd32b2f32d814b8116e84cf2b17347ebc1800181c4e2a1fb8dd53e1c635518c7dac47e9")>]
let ``PBKDF2 SHA256 encyrption should generate the expected derived key``(password:string, salt:string, iterations:int, keyLength:int, key:string) =
    let passwordBytes = Encoding.UTF8.GetBytes password
    let saltBytes = Encoding.UTF8.GetBytes salt
    
    let pbkdf2Hash = Crypto.pbkdf2 passwordBytes saltBytes iterations keyLength
    let pbkdf2HashHex = pbkdf2Hash |> Array.fold (fun state x -> state + sprintf "%02x" x) ""

    pbkdf2Hash.Length |> should equal keyLength
    pbkdf2HashHex |> should equal key