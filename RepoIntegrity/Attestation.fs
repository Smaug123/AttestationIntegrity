namespace RepoIntegrity

open System
open System.Text.Json.Serialization

type TLogId =
    {
        [<JsonPropertyName "keyId">]
        KeyId : string
    }

type TLogKindVersion =
    {
        [<JsonPropertyName "kind">]
        Kind : string
        [<JsonPropertyName "version">]
        Version : string
    }

type TLogInclusionPromise =
    {
        [<JsonPropertyName "signedEntryTimestamp">]
        SignedEntryTimestamp : string
    }

type TLogCheckpoint =
    {
        [<JsonPropertyName "envelope">]
        Envelope : string
    }

type TLogInclusionProof =
    {
        [<JsonPropertyName "logIndex">]
        LogIndex : string
        [<JsonPropertyName "rootHash">]
        RootHash : string
        [<JsonPropertyName "treeSize">]
        TreeSize : string
        [<JsonPropertyName "hashes">]
        Hashes : string list
        [<JsonPropertyName "checkpoint">]
        Checkpoint : TLogCheckpoint
    }

type TLogEntry =
    {
        [<JsonPropertyName "logIndex">]
        LogIndex : string
        [<JsonPropertyName "logId">]
        LogId : TLogId
        [<JsonPropertyName "kindVersion">]
        KindVersion : TLogKindVersion
        [<JsonPropertyName "integratedTime">]
        IntegratedTime : string
        [<JsonPropertyName "inclusionPromise">]
        InclusionPromise : TLogInclusionPromise
        [<JsonPropertyName "inclusionProof">]
        InclusionProof : TLogInclusionProof
        [<JsonPropertyName "canonicalizedBody">]
        CanonicalizedBody : string
    }

type VerificationCertificate =
    {
        [<JsonPropertyName "rawBytes">]
        RawBytes : string
    }

type VerificationMaterial =
    {
        [<JsonPropertyName "certificate">]
        Certificate : VerificationCertificate
        [<JsonPropertyName "tlogEntries">]
        TLogEntries : TLogEntry list
        [<JsonPropertyName "timestampVerificationData">]
        TimestampVerificationData : obj
    }

type AttestationBundle =
    {
        [<JsonPropertyName "mediaType">]
        MediaType : string
        [<JsonPropertyName "verificationMaterial">]
        VerificationMaterial : VerificationMaterial
    // TODO omitting DsseEnvelope because that looks parameterised
    }

type Attestation =
    {
        [<JsonPropertyName "bundle">]
        Bundle : AttestationBundle
    }

type Digest =
    {
        /// Optional.
        [<JsonPropertyName "sha256">]
        Sha256 : string
        /// Optional.
        [<JsonPropertyName "gitCommit">]
        GitCommit : string
    }

type Subject =
    {
        [<JsonPropertyName "name">]
        Name : string
        [<JsonPropertyName "digest">]
        Digest : Digest
    }

type WorkflowExternalParameters =
    {
        /// e.g. ".github/workflows/dotnet.yaml"
        [<JsonPropertyName "path">]
        Path : string
        /// e.g. "refs/heads/main"
        [<JsonPropertyName "ref">]
        Ref : string
        [<JsonPropertyName "repository">]
        Repository : Uri
    }

type ExternalParameters =
    {
        [<JsonPropertyName "workflow">]
        Workflow : WorkflowExternalParameters
    }

type GithubInternalParameters =
    {
        [<JsonPropertyName "event_name">]
        EventName : string
        [<JsonPropertyName "repository_id">]
        RepositoryId : string
        [<JsonPropertyName "repository_owner_id">]
        OwnerId : string
        [<JsonPropertyName "runner_environment">]
        RunnerEnvironment : string
    }

type InternalParameters =
    {
        [<JsonPropertyName "github">]
        Github : GithubInternalParameters
    }

type ResolvedDependency =
    {
        [<JsonPropertyName "digest">]
        Digest : Digest
        [<JsonPropertyName "uri">]
        Uri : Uri
    }

type BuildDefinition =
    {
        [<JsonPropertyName "buildtype">]
        BuildType : Uri
        [<JsonPropertyName "externalParameters">]
        ExternalParameters : ExternalParameters
        [<JsonPropertyName "internalParameters">]
        InternalParameters : InternalParameters
        [<JsonPropertyName "resolvedDependencies">]
        ResolvedDependencies : ResolvedDependency list
    }

type BuilderDetails =
    {
        [<JsonPropertyName "id">]
        Id : Uri
    }

type BuilderMetadata =
    {
        [<JsonPropertyName "invocationId">]
        InvocationId : Uri
    }

type RunDetails =
    {
        [<JsonPropertyName "builder">]
        Builder : BuilderDetails
        [<JsonPropertyName "metadata">]
        Metadata : BuilderMetadata
    }

type Predicate =
    {
        [<JsonPropertyName "buildDefinition">]
        BuildDefinition : BuildDefinition
        [<JsonPropertyName "runDetails">]
        RunDetails : RunDetails
    }

type VerificationStatement =
    {
        // Let's hope this never changes!
        [<JsonPropertyName "_type">]
        Type : Uri
        [<JsonPropertyName "predicateType">]
        PredicateType : Uri
        [<JsonPropertyName "subject">]
        Subject : Subject list
        [<JsonPropertyName "predicate">]
        Predicate : Predicate
    }

type VerificationCertificateSig =
    {
        [<JsonPropertyName "certificateIssuer">]
        CertificateIssuer : string
        [<JsonPropertyName "subjectAlternativeName">]
        San : string
        [<JsonPropertyName "issuer">]
        Issuer : Uri
        [<JsonPropertyName "githubWorkflowTrigger">]
        GithubWorkflowTrigger : string
        [<JsonPropertyName "githubWorkflowSHA">]
        GithubWorkflowSha : string
        [<JsonPropertyName "githubWorkflowName">]
        GithubWorkflowName : string
        [<JsonPropertyName "githubWorkflowRepository">]
        GithubWorkflowRepository : string
        [<JsonPropertyName "githubWorkflowRef">]
        GithubWorkflowRef : string
        [<JsonPropertyName "buildSignerURI">]
        BuildSignerUri : string
        [<JsonPropertyName "buildSignerDigest">]
        BuildSignerDigest : string
        [<JsonPropertyName "runnerEnvironment">]
        RunnerEnvironment : string
        [<JsonPropertyName "sourceRepositoryURI">]
        SourceRepositoryUri : Uri
        [<JsonPropertyName "sourceRepositoryDigest">]
        SourceRepositoryDigest : string
        [<JsonPropertyName "sourceRepositoryRef">]
        SourceRepositoryRef : string
        [<JsonPropertyName "sourceRepositoryIdentifier">]
        SourceRepositoryIdentifier : string
        [<JsonPropertyName "sourceRepositoryOwnerURI">]
        SourceRepositoryOwnerURI : Uri
        [<JsonPropertyName "sourceRepositoryOwnerIdentifier">]
        SourceRepositoryOwnerIdentifier : string
        [<JsonPropertyName "buildConfigURI">]
        BuildConfigUri : string
        [<JsonPropertyName "buildConfigDigest">]
        BuildConfigDigest : string
        [<JsonPropertyName "buildTrigger">]
        BuildTrigger : string
        [<JsonPropertyName "runInvocationURI">]
        RunInvocationUri : Uri
        [<JsonPropertyName "sourceRepositoryVisibilityAtSigning">]
        RepoVisibilityAtSigning : string
    }

type VerificationSignature =
    {
        [<JsonPropertyName "certificate">]
        Certificate : VerificationCertificateSig
    }

type VerificationTimestamp =
    {
        [<JsonPropertyName "type">]
        Type : string
        [<JsonPropertyName "uri">]
        Uri : string
        [<JsonPropertyName "timestamp">]
        Timestamp : DateTimeOffset
    }

type VerificationSan =
    {
        [<JsonPropertyName "subjectAlternativeName">]
        San : string
        [<JsonPropertyName "regexp">]
        Regex : string
    }

type VerificationIssuer =
    {
        [<JsonPropertyName "issuer">]
        Issuer : Uri
    }

type VerificationIdentity =
    {
        [<JsonPropertyName "subjectAlternativeName">]
        SubjectAlternativeName : VerificationSan
        [<JsonPropertyName "issuer">]
        Issuer : VerificationIssuer
    }

type VerificationResult =
    {
        [<JsonPropertyName "mediaType">]
        MediaType : string
        [<JsonPropertyName "statement">]
        Statement : VerificationStatement
        [<JsonPropertyName "signature">]
        Signature : VerificationSignature
        [<JsonPropertyName "verifiedTimestamps">]
        Timestamps : VerificationTimestamp list
        [<JsonPropertyName "verifiedIdentity">]
        Identity : VerificationIdentity
    }

type AttestationVerification =
    {
        [<JsonPropertyName "attestation">]
        Attestation : Attestation
        [<JsonPropertyName "verificationResult">]
        VerificationResult : VerificationResult
    }
