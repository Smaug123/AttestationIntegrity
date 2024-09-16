namespace RepoIntegrity

open System.Collections.Generic
open Octokit

type GitHubVerifyFailure =
    | SignatureUnverified of commitHash : string
    | CommitByUnrecognisedUser of commitHash : string * userId : int64 * currentUserName : string

[<RequireQualifiedAccess>]
module GitHubVerify =
    // Ruleset are not yet supported in Octokit :(
    // https://github.com/octokit/octokit.net/issues/2918

    let private usersCache = Dictionary<string, int64> ()

    let simplifyAllowedUsernames (client : IGitHubClient) (allowedUsers : string list) : Async<int64 Set> =
        allowedUsers
        |> List.map (fun name ->
            match lock usersCache (fun () -> usersCache.TryGetValue name) with
            | true, v -> async.Return v
            | false, _ ->
                async {
                    let! user = client.User.Get name |> Async.AwaitTask
                    lock usersCache (fun () -> usersCache.[name] <- user.Id)
                    return user.Id
                }
        )
        |> Async.Parallel
        |> Async.map Set.ofArray

    let verify
        (client : IGitHubClient)
        (repo : Repo<int64>)
        (allowedUsers : string list)
        (commitHash : string)
        (ref : string)
        : Result<unit, GitHubVerifyFailure list> Async
        =
        async {
            let errors = ResizeArray ()
            let! commit = client.Repository.Commit.Get (repo.RepoId, commitHash) |> Async.AwaitTask

            if not commit.Commit.Verification.Verified then
                errors.Add (GitHubVerifyFailure.SignatureUnverified commit.Commit.Sha)

            let! allowedUsers = simplifyAllowedUsernames client allowedUsers

            if not (allowedUsers.Contains commit.User.Id) then
                errors.Add (
                    GitHubVerifyFailure.CommitByUnrecognisedUser (commitHash, commit.User.Id, commit.User.Login)
                )

            return
                if errors.Count = 0 then
                    Ok ()
                else
                    Error (Seq.toList errors)
        }
