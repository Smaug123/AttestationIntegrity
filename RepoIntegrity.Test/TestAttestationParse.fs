namespace RepoIntegrity.Test

open System.Text.Json
open NUnit.Framework
open RepoIntegrity

[<TestFixture>]
module TestAttestationParse =
    [<Test>]
    let ``Test parse`` () =
        let source = EmbeddedResource.get "example.json"
        let output = JsonSerializer.Deserialize<AttestationVerification list> source
        ()
