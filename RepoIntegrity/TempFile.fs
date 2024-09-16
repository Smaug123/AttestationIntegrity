namespace RepoIntegrity

open System
open System.IO.Abstractions

type TempFile (fs : IFileSystem) =
    let file = fs.Path.GetTempFileName () |> fs.FileInfo.New

    member _.File : IFileInfo = file

    interface IDisposable with
        member _.Dispose () =
            try
                file.Delete ()
            with _ ->
                ()
