namespace RepoIntegrity

open System.IO.Abstractions
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open NuGet.Protocol
open NuGet.Protocol.Core.Types
open NuGet.Versioning

[<RequireQualifiedAccess>]
module NuGet =
    // astounding stuff here
    let private toLogLevel (level : NuGet.Common.LogLevel) : LogLevel =
        match level with
        | NuGet.Common.LogLevel.Debug -> LogLevel.Debug
        | NuGet.Common.LogLevel.Error -> LogLevel.Error
        | NuGet.Common.LogLevel.Information -> LogLevel.Information
        | NuGet.Common.LogLevel.Minimal -> LogLevel.Trace
        | NuGet.Common.LogLevel.Warning -> LogLevel.Warning
        | NuGet.Common.LogLevel.Verbose -> LogLevel.Trace
        | level -> failwith $"Exhaustive enum match: %O{level}"

    let private createLogger (logger : ILogger) : NuGet.Common.ILogger =
        { new NuGet.Common.ILogger with
            member this.LogDebug (data) = logger.LogDebug data
            member this.LogVerbose (data) = logger.LogTrace data
            member this.LogInformation (data) = logger.LogInformation data
            member this.LogMinimal (data) = logger.LogTrace data
            member this.LogWarning (data) = logger.LogWarning data
            member this.LogError (data) = logger.LogError data
            member this.LogInformationSummary (data) = logger.LogInformation data
            member this.Log (level, data) = logger.Log (toLogLevel level, data)

            member this.LogAsync (level, data) =
                task { logger.Log (toLogLevel level, data) }

            member this.Log (message) =
                logger.Log (toLogLevel message.Level, message.Message)

            member this.LogAsync (message) =
                task { logger.Log (toLogLevel message.Level, message.Message) }
        }

    let getAllPackageVersions (lf : ILoggerFactory) (packageName : string) : _ seq Async =
        async {
            let logger = lf.CreateLogger "GetPackageVersions"
            use cache = new SourceCacheContext ()
            let repo = Repository.Factory.GetCoreV3 "https://api.nuget.org/v3/index.json"
            let! ct = Async.CancellationToken

            let! resource = repo.GetResourceAsync<FindPackageByIdResource> ct |> Async.AwaitTask

            let! versions =
                resource.GetAllVersionsAsync (packageName, cache, createLogger logger, ct)
                |> Async.AwaitTask

            return versions
        }

    let downloadArtefact
        (lf : ILoggerFactory)
        (toDest : IFileInfo)
        (package : string)
        (version : NuGetVersion)
        : unit Async
        =
        async {
            let logger = lf.CreateLogger "GetPackageVersions"
            use cache = new SourceCacheContext ()
            let repo = Repository.Factory.GetCoreV3 "https://api.nuget.org/v3/index.json"
            let! ct = Async.CancellationToken

            let! resource = repo.GetResourceAsync<FindPackageByIdResource> ct |> Async.AwaitTask

            use stream = toDest.OpenWrite ()

            let! result =
                resource.CopyNupkgToStreamAsync (package, version, stream, cache, createLogger logger, ct)
                |> Async.AwaitTask

            // Naturally there are no docstrings, but I imagine this is correct:
            if not result then
                failwith $"Failed to download artefact %s{package} at version %O{version}"
        }
