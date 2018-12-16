open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Orleans
open Orleans.Runtime.Configuration
open Orleans.Hosting
open System
open FSharp.Control.Tasks
open SharpTopics.FsInterfaces
open SharpTopics.FsGrains

let buildSiloHost() =
    let builder = new SiloHostBuilder()
    builder
        .UseLocalhostClustering()
        .ConfigureApplicationParts(fun parts ->
            parts
                .AddApplicationPart(typeof<IHello>.Assembly)
                .AddApplicationPart(typeof<HelloGrain>.Assembly)
                .WithCodeGeneration()
            |> ignore
        )
        .ConfigureLogging(fun logging ->
            logging.AddConsole() |> ignore
        )
        //.ConfigureServices(fun services ->
        //    services.AddSing
        //)
        .Build()

[<EntryPoint>]
let main _ =
    let t = task {
        let host = buildSiloHost()
        do! host.StartAsync()

        printfn "Press any keys to terminate..."
        Console.Read() |> ignore

        do! host.StopAsync()

        printfn "Silohost is stopped"
    }
    
    t.Wait()

    0 // return an integer exit code
