namespace RepoIntegrity

[<RequireQualifiedAccess>]
module Result =

    let allOkOrError<'o, 'e> (s : Result<'o, 'e> seq) : Result<'o list, 'o list * 'e list> =
        let oks = ResizeArray ()
        let errs = ResizeArray ()

        for s in s do
            match s with
            | Ok o -> oks.Add o
            | Error e -> errs.Add e

        let oks = List.ofSeq oks

        if errs.Count = 0 then
            Ok oks
        else
            Error (oks, Seq.toList errs)

    let bimap<'ok, 'err, 'a, 'b> (f : 'ok -> 'a) (g : 'err -> 'b) (r : Result<'ok, 'err>) : Result<'a, 'b> =
        match r with
        | Ok x -> f x |> Ok
        | Error y -> g y |> Error

    let cata<'a, 'b, 'ret> (f : 'a -> 'ret) (g : 'b -> 'ret) (r : Result<'a, 'b>) : 'ret =
        match r with
        | Ok a -> f a
        | Error b -> g b
