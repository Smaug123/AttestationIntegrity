namespace RepoIntegrity.Test

open System
open System.IO
open System.Reflection

[<RequireQualifiedAccess>]
module EmbeddedResource =

    let get (name : string) : string =
        let assy = Assembly.GetExecutingAssembly ()

        let name =
            assy.GetManifestResourceNames ()
            |> Seq.choose (fun s ->
                if s.EndsWith (name, StringComparison.OrdinalIgnoreCase) then
                    Some s
                else
                    None
            )
            |> Seq.exactlyOne

        use s = assy.GetManifestResourceStream name
        use reader = new StreamReader (s, leaveOpen = true)

        reader.ReadToEnd ()
