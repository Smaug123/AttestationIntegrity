namespace RepoIntegrity

open System
open System.IO.Abstractions
open System.Text.Json
open Microsoft.Extensions.Logging
open NuGet.Versioning
open Octokit

type NuGetArtefact =
    {
        /// e.g. "WoofWare.Myriad.Plugins"
        Name : string
    }

type BinaryArtefact = | NuGet of NuGetArtefact

type AttestationVerifyError =
    /// `gh attestation verify` failed.
    | FailedExternal of ProcessOutput<int> list
    /// We couldn't parse GitHub's `gh attestation verify` successful output.
    | CouldNotParse of exn
    /// Contains a human-readable description of why, even though GitHub said this
    /// was correctly attested, that attestation was not sufficient for us.
    | FailedInternal of string
    /// Somehow there were multiple attestations in response to `gh attestation verify`.
    | MultipleAttestations

    /// Human-readable description.
    override this.ToString () =
        match this with
        | AttestationVerifyError.MultipleAttestations ->
            "Unexpectedly received multiple attestations in response to `gh attestation verify`."
        | AttestationVerifyError.FailedExternal processOutputs ->
            processOutputs
            |> List.map (fun po -> $"exit code %i{po.ExitCode}:\nStdout:\n%s{po.Stdout}\nStderr:\n%s{po.Stderr}")
            |> String.concat "\n"
            |> fun s -> $"`gh attestation verify` failed:\n%s{s}"
        | AttestationVerifyError.CouldNotParse exc ->
            $"Could not parse output of `gh attestation verify`: %s{exc.Message}"
        | AttestationVerifyError.FailedInternal reason ->
            $"Artefact successfully attested, but failed our own subsequent validation: %s{reason}"

type NuGetVerificationError =
    {
        Repo : Repo<unit>
        Artefact : NuGetArtefact
        Version : NuGetVersion
        Output : AttestationVerifyError
    }

    override this.ToString () =
        $"Could not verify artefact %s{this.Artefact.Name} at version %O{this.Version}: %O{this.Output}"

type AttestationTarget =
    {
        Repo : Repo<unit>
        Artefact : BinaryArtefact
        AllowedUsers : string list
        /// Perhaps the artefact only became attested at some particular point in time.
        /// This lets you say "yep we're fine with earlier versions not being attested", for example.
        ExpectAttestation : Version -> bool
    }

module Program =

    let registeredRepos : AttestationTarget list =
        [
            {
                Repo = Repo.parse "Smaug123/WoofWare.Myriad" |> Option.get
                Artefact =
                    BinaryArtefact.NuGet
                        {
                            Name = "WoofWare.Myriad.Plugins"
                        }
                ExpectAttestation = fun version -> version >= Version (2, 1, 45)
                AllowedUsers = [ "Smaug123" ]
            }
            {
                Repo = Repo.parse "Smaug123/WoofWare.Myriad" |> Option.get
                Artefact =
                    BinaryArtefact.NuGet
                        {
                            Name = "WoofWare.Myriad.Plugins.Attributes"
                        }
                ExpectAttestation = fun version -> version >= Version (3, 1, 7)
                AllowedUsers = [ "Smaug123" ]
            }

        ]

    let checkGhPresent () : unit Async =
        async {
            let! output = Process.run "gh" [ "--version" ]

            match output with
            | Ok _ -> ()
            | Error _ -> failwith "Install `gh` to continue."
        }

    /// `gh attestation verify` is *super* flaky.
    let rec verifyAttestation
        (client : IGitHubClient)
        (attempts : ProcessOutput<int> list)
        (runProcess : string -> string list -> ProcessResult Async)
        (org : string)
        (tempFile : IFileInfo)
        =
        if attempts.Length > 3 then
            async.Return (Error (AttestationVerifyError.FailedExternal attempts))
        else
            async {
                let! output =
                    runProcess
                        "gh"
                        [
                            "attestation"
                            "verify"
                            "--owner"
                            org
                            "--format"
                            "json"
                            tempFile.FullName
                        ]

                match output with
                | Error e -> return! verifyAttestation client (e :: attempts) runProcess org tempFile
                | Ok output ->
                    let attestation =
                        try
                            JsonSerializer.Deserialize<AttestationVerification list> output.Stdout |> Ok
                        with e ->
                            Error (AttestationVerifyError.CouldNotParse e)

                    match attestation with
                    | Error e -> return Error e
                    | Ok attestation ->

                    match attestation |> List.tryExactlyOne with
                    | None -> return Error AttestationVerifyError.MultipleAttestations
                    | Some attestation ->

                    match! ParsedAttestation.Parse client attestation with
                    | Error e -> return Error (AttestationVerifyError.FailedInternal e)
                    | Ok attestation ->

                    return Ok attestation
            }

    let processSingleNuGetVersion
        (lf : ILoggerFactory)
        (fs : IFileSystem)
        (client : IGitHubClient)
        (runProcess : string -> string list -> ProcessResult Async)
        (repo : Repo<unit>)
        (artefact : NuGetArtefact)
        (version : NuGetVersion)
        : Result<_, _> Async
        =
        async {
            use tempFile = new TempFile (fs)
            do! NuGet.downloadArtefact lf tempFile.File artefact.Name version

            let! output = verifyAttestation client [] runProcess repo.Org tempFile.File

            match output with
            | Error e ->
                return
                    {
                        Repo = repo
                        Artefact = artefact
                        Version = version
                        Output = e
                    }
                    |> Error
            | Ok attestation -> return Ok (artefact, version, attestation)
        }

    let processNuGet
        (lf : ILoggerFactory)
        (fs : IFileSystem)
        (client : IGitHubClient)
        (runProcess : string -> string list -> ProcessResult Async)
        (repo : Repo<unit>)
        (expectAttestation : Version -> bool)
        (artefact : NuGetArtefact)
        : Result<_, _ * NuGetVerificationError list> Async
        =
        async {
            let! versions = NuGet.getAllPackageVersions lf artefact.Name

            let! results =
                versions
                |> Seq.filter (fun v ->
                    let version = Version (v.Major, v.Minor, v.Patch)
                    expectAttestation version
                )
                |> Seq.map (processSingleNuGetVersion lf fs client runProcess repo artefact)
                |> Async.Parallel

            let results = results |> Result.allOkOrError
            return results
        }

    [<EntryPoint>]
    let main argv =
        let lf = LoggerFactory.console LogLevel.Information
        let fs = FileSystem ()
        let client = ProductHeaderValue "WoofWare.RepoIntegrity" |> GitHubClient

        match Environment.GetEnvironmentVariable "OCTOKIT_CREDS" with
        | null -> ()
        | token -> client.Credentials <- Credentials token

        checkGhPresent () |> Async.RunSynchronously

        let attestationResults =
            registeredRepos
            |> List.groupBy (fun target -> target.Repo)
            |> List.collect (fun (repo, artefact) ->
                artefact
                |> List.map (fun art ->
                    match art.Artefact with
                    | BinaryArtefact.NuGet nuGet ->
                        processNuGet lf fs client Process.run repo art.ExpectAttestation nuGet
                        |> Async.map (
                            Result.map (List.map (fun (art', ver, att) -> art.AllowedUsers, art', ver, att))
                        )
                        |> Async.map (
                            Result.mapError (
                                Tuple.lmap (List.map (fun (art', ver, att) -> art.AllowedUsers, art', ver, att))
                            )
                        )
                )
            )
            |> Async.Parallel
            |> Async.RunSynchronously
            |> Result.allOkOrError

        let mutable exitCode = 0

        let attestationResults =
            match attestationResults with
            | Ok oks -> List.concat oks
            | Error (oks, errs) ->
                exitCode <- 1
                let oks = errs |> List.collect fst |> (fun result -> result @ (List.concat oks))
                let errs = errs |> List.collect snd

                for err in errs do
                    Console.WriteLine $"%O{err.Repo} %s{err.Artefact.Name} %O{err.Version}: FAIL"

                    Console.Error.WriteLine
                        $"Failed to validate %s{err.Artefact.Name} at %O{err.Version} in %O{err.Repo}: %O{err.Output}"

                oks

        // Now check the source repositories have the properties we expect of them.
        attestationResults
        |> List.map (fun (allowedUsers, art, ver, att) ->
            GitHubVerify.verify
                client
                att.SourceRevision.Repo
                allowedUsers
                att.SourceRevision.GitHash
                att.SourceRevision.GitRef
        )
        |> Async.Parallel
        |> Async.map Result.allOkOrError
        |> Async.map (Result.bimap (List.iter id) (Tuple.bimap (List.iter id) List.concat))
        |> Async.map (
            Result.cata
                id
                (Tuple.cata (fun () v ->
                    Console.Error.WriteLine $"%O{v}"
                    exitCode <- 2
                ))
        )
        |> Async.RunSynchronously

        0
