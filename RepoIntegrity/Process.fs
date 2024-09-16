namespace RepoIntegrity

open System.Diagnostics
open System.Threading.Tasks

type ProcessOutput<'exit> =
    {
        Stdout : string
        Stderr : string
        ExitCode : 'exit
    }

type ProcessResult = Result<ProcessOutput<unit>, ProcessOutput<int>>

[<RequireQualifiedAccess>]
module Process =
    let run (name : string) (args : string list) : ProcessResult Async =
        async {
            let psi = ProcessStartInfo name

            for arg in args do
                psi.ArgumentList.Add arg

            psi.RedirectStandardError <- true
            psi.RedirectStandardOutput <- true

            use proc = new Process ()
            proc.StartInfo <- psi

            let stdoutFinished = TaskCompletionSource ()
            let stderrFinished = TaskCompletionSource ()
            let stdout = ResizeArray ()
            let stderr = ResizeArray ()

            proc.ErrorDataReceived.Add (fun s ->
                if isNull s.Data then
                    stderrFinished.SetResult ()
                else
                    stderr.Add s.Data
            )

            proc.OutputDataReceived.Add (fun s ->
                if isNull s.Data then
                    stdoutFinished.SetResult ()
                else
                    stdout.Add s.Data
            )

            proc.Start () |> ignore<bool>
            proc.BeginErrorReadLine ()
            proc.BeginOutputReadLine ()

            let! ct = Async.CancellationToken

            do! proc.WaitForExitAsync ct |> Async.AwaitTask

            do! stdoutFinished.Task |> Async.AwaitTask
            do! stderrFinished.Task |> Async.AwaitTask

            let stdout = stdout |> String.concat "\n" |> (fun s -> s.Trim ())
            let stderr = stderr |> String.concat "\n" |> (fun s -> s.Trim ())

            if proc.ExitCode = 0 then
                return
                    {
                        Stdout = stdout
                        Stderr = stderr
                        ExitCode = ()
                    }
                    |> Ok
            else
                return
                    {
                        Stdout = stdout
                        Stderr = stderr
                        ExitCode = proc.ExitCode
                    }
                    |> Error
        }
