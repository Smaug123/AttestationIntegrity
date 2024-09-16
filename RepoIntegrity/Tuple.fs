namespace RepoIntegrity

[<RequireQualifiedAccess>]
module Tuple =
    let bimap<'a, 'b, 'x, 'y> (f : 'a -> 'x) (g : 'b -> 'y) (a : 'a, b : 'b) = f a, g b

    let lmap<'a, 'b, 'x> (f : 'a -> 'x) (a : 'a, b : 'b) = (f a, b)
    let rmap<'a, 'b, 'y> (g : 'b -> 'y) (a : 'a, b : 'b) = (a, g b)

    let cata<'a, 'b, 'ret> (f : 'a -> 'b -> 'ret) (x : 'a, y : 'b) = f x y
