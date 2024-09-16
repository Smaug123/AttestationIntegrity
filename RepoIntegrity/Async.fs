namespace RepoIntegrity

[<RequireQualifiedAccess>]
module Async =

    let map<'a, 'b> (f : 'a -> 'b) (r : Async<'a>) : Async<'b> =
        async {
            let! r = r
            return f r
        }
