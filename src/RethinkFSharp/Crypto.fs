namespace RethinkFSharp.Network

open System.Security.Cryptography
open System.Text
open Org.BouncyCastle.Crypto.Generators
open Org.BouncyCastle.Crypto.Digests
open Org.BouncyCastle.Crypto.Parameters

module internal Crypto =
    let randomNonce numBytes =
        use randomGenerator = new RNGCryptoServiceProvider()
        let byteArray = Array.zeroCreate numBytes
        randomGenerator.GetBytes byteArray
        byteArray

    let sha256 bytes =
        use sha256 = SHA256.Create()
        sha256.TransformBlock(bytes, 0, bytes.Length, null, 0) |> ignore
        sha256.TransformFinalBlock(Array.zeroCreate 1, 0, 0) |> ignore
        sha256.Hash

    let hmac256 key (text:string) =
        use hmac256 = new HMACSHA256(key)
        hmac256.ComputeHash(Encoding.UTF8.GetBytes text)

    let pbkdf2 password salt iterations keyLength =
        let pbkdf2Generator = new Pkcs5S2ParametersGenerator(new Sha256Digest())
        pbkdf2Generator.Init(password, salt, iterations)
        let derivedKey = pbkdf2Generator.GenerateDerivedParameters("AES", keyLength * 8) :?> KeyParameter
        derivedKey.GetKey()


    let xor (left:byte[]) (right:byte[]) =
        Array.init left.Length (fun i -> left.[i] ^^^ right.[i])
