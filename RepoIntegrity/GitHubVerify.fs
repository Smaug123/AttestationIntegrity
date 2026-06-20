namespace RepoIntegrity

open System
open System.Collections.Generic
open Octokit

type GitUser =
    {
        DateTimeOffset : DateTimeOffset
        Name : string
        Email : string
    }

    static member Parse (s : string) : GitUser option =
        match s.Split '<' with
        | [| before ; after |] ->
            let before = before.TrimEnd ()

            match after.Split '>' with
            | [| email ; after |] ->
                let after = after.TrimStart().Split ' '

                match after with
                | [| unixTime ; _offset |] ->
                    // We'll ignore the local timezone; this is a Unix timestamp and indicates
                    // the correct instant.
                    match Int64.TryParse unixTime with
                    | false, _ -> None
                    | true, unixTime ->

                    {
                        DateTimeOffset = DateTimeOffset.FromUnixTimeSeconds unixTime
                        Name = before
                        Email = email
                    }
                    |> Some
                | _ -> None
            | _ -> None
        | _ -> None

type GitHubVerifyFailure =
    | SignatureUnverified of commitHash : string
    | InvalidVerification of commitHash : string * verification : string
    | CommitByNoUser of commitHash : string
    | CouldNotLookUp of commitHash : string * user : GitUser
    | CommitByUnrecognisedUser of commitHash : string * userId : int64 * currentUserName : string

/// This is basically a Git commit object, but it came from the GitHub API as being verified.
type Verification =
    {
        /// The SHA of the tree object which this commit points to.
        TreeHash : string
        /// Parent commits to this one; usually there is only one, and indeed one of our integrity
        /// criteria is that history is linear.
        Parents : string list
        /// Author of the commit. (This is the Git concept.)
        Author : GitUser
        /// Committer of the commit. (This is the Git concept.)
        Committer : GitUser
        /// Commit message.
        Message : string
    }

    /// Parse a Git commit object.
    static member Parse (s : string) : Result<Verification, string> =
        let lines = s.Split '\n'
        let mutable tree = None
        let parents = ResizeArray ()
        let mutable author = None
        let mutable committer = None
        let mutable message = None

        let rec go (i : int) =
            if i >= lines.Length then
                Error $"Somehow didn't have a commit message! %s{s}"
            else

            let line = lines.[i]

            if String.IsNullOrEmpty line then
                message <- Some (lines.[i + 1 ..] |> String.concat "\n")
                Ok ()
            else if

                line.StartsWith ("tree ", StringComparison.Ordinal)
            then
                match tree with
                | None ->
                    tree <- Some (line.Substring "tree ".Length)
                    go (i + 1)
                | Some _ -> Error $"duplicate trees: %s{s}"
            elif line.StartsWith ("parent ", StringComparison.Ordinal) then
                parents.Add (line.Substring "parent ".Length)
                go (i + 1)
            elif line.StartsWith ("author", StringComparison.Ordinal) then
                match author with
                | None ->
                    author <- Some (line.Substring "author ".Length)
                    go (i + 1)
                | Some _ -> Error $"duplicate authors: %s{s}"
            elif line.StartsWith ("committer", StringComparison.Ordinal) then
                match committer with
                | None ->
                    committer <- Some (line.Substring "committer ".Length)
                    go (i + 1)
                | Some _ -> Error $"duplicate committer: %s{s}"
            else
                Error $"unrecognised line\n%s{line}\nin %s{s}"

        match go 0 with
        | Error s -> Error s
        | Ok () ->

        match author with
        | None -> Error $"No author in %s{s}"
        | Some author ->

        match GitUser.Parse author with
        | None -> Error $"Could not parse author %s{author}"
        | Some author ->

        match committer with
        | None -> Error $"No committer in %s{s}"
        | Some committer ->

        match GitUser.Parse committer with
        | None -> Error $"Could not parse author %s{committer}"
        | Some committer ->

        match message with
        | None -> Error $"No message in %s{s}"
        | Some message ->

        match tree with
        | None -> Error $"No tree in %s{s}"
        | Some tree ->

        {
            TreeHash = tree
            Parents = parents |> Seq.toList
            Author = author
            Committer = committer
            Message = message
        }
        |> Ok

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

            match commit with
            | null -> return failwith $"Got null Commit for %s{commitHash} on repo %s{repo.Org}/%s{repo.Repo}"
            | commit ->

            if not commit.Commit.Verification.Verified then
                errors.Add (GitHubVerifyFailure.SignatureUnverified commit.Commit.Sha)

            let! allowedUsers = simplifyAllowedUsernames client allowedUsers

            let verification =
                match commit.Commit.Verification with
                | null ->
                    errors.Add (GitHubVerifyFailure.SignatureUnverified commitHash)
                    None
                | verification ->
                    if not verification.Verified then
                        errors.Add (GitHubVerifyFailure.SignatureUnverified commitHash)

                    match Verification.Parse verification.Payload with
                    | Error e ->
                        errors.Add (GitHubVerifyFailure.InvalidVerification (commitHash, e))
                        None
                    | Ok v -> Some v

            let commitUser =
                match commit.User with
                | null ->
                    // Bots might not have a `User`, but we can still extract the verified user.
                    match verification with
                    | None -> None
                    | Some v -> Some (Choice2Of2 v)
                | u -> Some (Choice1Of2 u)

            let! commitUserId =
                match commitUser with
                | None -> async.Return None
                | Some (Choice1Of2 u) -> async.Return (Some u.Id)
                | Some (Choice2Of2 u) ->
                    async {
                        let req = SearchUsersRequest u.Author.Email
                        let! searchResult = client.Search.SearchUsers req |> Async.AwaitTask

                        match searchResult.Items |> Seq.tryExactlyOne with
                        | None ->
                            errors.Add (GitHubVerifyFailure.CouldNotLookUp (commitHash, u.Author))
                            return None
                        | Some user -> return Some user.Id
                    }

            match commitUserId with
            | None -> errors.Add (GitHubVerifyFailure.CommitByNoUser commitHash)
            | Some user ->
                if not (allowedUsers.Contains user) then
                    errors.Add (
                        GitHubVerifyFailure.CommitByUnrecognisedUser (commitHash, commit.User.Id, commit.User.Login)
                    )

            return
                if errors.Count = 0 then
                    Ok ()
                else
                    Error (Seq.toList errors)
        }
