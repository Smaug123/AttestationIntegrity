namespace RepoIntegrity

open System
open Octokit

type Sha256 =
    private
    | Sha256 of string

    static member Parse (s : string) =
        if s.Length <> 64 then
            failwith $"String %s{s} must be 64 hex chars"

        let s = s.ToLowerInvariant ()
        let badChars = s.ToCharArray () |> Array.filter (not << Char.IsAsciiHexDigitLower)

        if badChars.Length > 0 then
            failwith $"String had noncompliant chars: %s{s}"

        Sha256 s

type RepoVisibility =
    | Public

    static member Parse (s : string) : RepoVisibility option =
        match s.ToLowerInvariant () with
        | "public" -> Some RepoVisibility.Public
        | _ -> None

type SourceRevision =
    {
        Repo : Repo<int64>
        GitHash : string
        /// e.g. "refs/heads/main"
        GitRef : string
    }

type ParsedAttestation =
    {
        VerifiedAt : DateTimeOffset list
        /// What this artefact was named when it was attested.
        ArtefactName : string
        /// SHA256, a 64-character hex string
        Sha256 : Sha256
        SourceRevision : SourceRevision
        RunInvocation : Uri
        Visibility : RepoVisibility
    }

    static member Parse
        (client : IGitHubClient)
        (att : AttestationVerification)
        : Result<ParsedAttestation, string> Async
        =
        let verifiedAt =
            att.VerificationResult.Timestamps |> List.map (fun ts -> ts.Timestamp)

        match att.VerificationResult.Statement.Subject with
        | [] -> Error "Expected a verificationResult.statement but got none" |> async.Return
        | _ :: _ :: _ ->
            Error "Expected exactly one verificationResult.statement but got multiple"
            |> async.Return
        | [ subject ] ->

        match att.VerificationResult.Signature.Certificate.SourceRepositoryUri |> Repo.parse' with
        | None ->
            Error
                $"Could not parse %O{att.VerificationResult.Signature.Certificate.SourceRepositoryUri} as a GitHub repo"
            |> async.Return
        | Some sourceRepo ->

        let visibility =
            att.VerificationResult.Signature.Certificate.RepoVisibilityAtSigning

        match RepoVisibility.Parse visibility with
        | None ->
            Error $"Unrecognised sourceRepositoryVisibilityAtSigning: %s{visibility}"
            |> async.Return
        | Some visibility ->

        if
            att.VerificationResult.Signature.Certificate.RunnerEnvironment
            <> "github-hosted"
        then
            Error $"Unrecognised runner environment: %s{att.VerificationResult.Signature.Certificate.RunnerEnvironment}"
            |> async.Return
        else

        match Int64.TryParse att.VerificationResult.Signature.Certificate.SourceRepositoryIdentifier with
        | false, _ ->
            Error
                $"Could not parse %s{att.VerificationResult.Signature.Certificate.SourceRepositoryIdentifier} as an int64 repo ID"
            |> async.Return
        | true, repoId ->

        async {
            let! githubRepo = client.Repository.Get (sourceRepo.Org, sourceRepo.Repo) |> Async.AwaitTask

            if githubRepo.Id <> repoId then
                return
                    Error
                        $"Repo ID %i{repoId} reported from attestation is not the same as current repo ID %i{githubRepo.Id}"
            else

            let sourceRevision =
                {
                    Repo = sourceRepo |> Repo.map (fun () -> repoId)
                    GitHash = att.VerificationResult.Signature.Certificate.SourceRepositoryDigest
                    GitRef = att.VerificationResult.Signature.Certificate.SourceRepositoryRef
                }

            return
                {
                    VerifiedAt = verifiedAt
                    ArtefactName = subject.Name
                    Sha256 = subject.Digest.Sha256 |> Sha256.Parse
                    SourceRevision = sourceRevision
                    RunInvocation = att.VerificationResult.Signature.Certificate.RunInvocationUri
                    Visibility = visibility
                }
                |> Ok
        }
