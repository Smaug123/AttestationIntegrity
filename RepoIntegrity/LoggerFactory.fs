namespace RepoIntegrity

open System
open Microsoft.Extensions.Logging

[<RequireQualifiedAccess>]
module LoggerFactory =
    let console (logLevel : LogLevel) =
        { new ILoggerFactory with
            member _.Dispose () = ()

            member this.CreateLogger name =
                { new ILogger with
                    member _.IsEnabled level = level >= logLevel

                    member this.Log (level, _, state, exc, formatter) =
                        if level >= logLevel then
                            let toWrite = formatter.Invoke (state, exc)
                            Console.Error.WriteLine $"%s{name}: %s{toWrite}"

                    member this.BeginScope _ =
                        { new IDisposable with
                            member _.Dispose () = ()
                        }
                }

            member this.AddProvider _ = failwith "ignored"
        }
