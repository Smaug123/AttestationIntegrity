namespace RepoIntegrity

open System

/// A repo hosted on GitHub.com.
type Repo<'RepoId> =
    {
        /// e.g. "Smaug123" or "G-Research"
        Org : string
        /// e.g. "WoofWare.Myriad" or "ApiSurface"
        Repo : string
        RepoId : 'RepoId
    }

    override this.ToString () =
        $"https://github.com/%s{this.Org}/%s{this.Repo}"

[<RequireQualifiedAccess>]
module Repo =
    let parse (s : string) : Repo<unit> option =
        let split = s.Split '/'

        match split with
        | [| org ; repo |] ->
            {
                Org = org
                Repo = repo
                RepoId = ()
            }
            |> Some
        | _ -> None

    let parse' (u : Uri) : Repo<unit> option =
        let path =
            match u.Host with
            | "github.com"
            | "www.github.com" -> u.AbsolutePath.TrimStart '/'
            | _ -> failwith $"URI %O{u} did not have a recognised GitHub host"

        parse path

    let map<'a, 'b> (f : 'a -> 'b) (r : Repo<'a>) : Repo<'b> =
        {
            Org = r.Org
            Repo = r.Repo
            RepoId = f r.RepoId

        }
