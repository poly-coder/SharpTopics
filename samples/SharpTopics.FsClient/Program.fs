open Microsoft.Extensions.Logging
open Orleans
open Orleans.Runtime.Configuration
open Orleans.Hosting
open System
open FSharp.Control.Tasks
open SharpTopics.FsInterfaces
open System.Threading.Tasks

let buildClient() =
    let builder = new ClientBuilder()
    builder
        .UseLocalhostClustering()
        .ConfigureApplicationParts(fun parts ->
            parts.AddApplicationPart(typeof<IHello>.Assembly).WithCodeGeneration() |> ignore
        )
        .ConfigureLogging(fun logging ->
            logging.AddConsole() |> ignore
        )
        .Build()

let worker (client: IClusterClient) = task {
    let friend = client.GetGrain<IHello> 0L
    let! response = friend.SayHello "Good morning, my friend!"
    printfn "%s" response
}

[<EntryPoint>]
let main argv =
    let t = task {
        use client = buildClient()
        do! client.Connect(fun exn -> task {
            do! Task.Delay(1000)
            return true
        })
        printfn "client successfully connect to silo host"
        do! worker client
    }

    t.Wait()

    Console.ReadKey() |> ignore

    0 // return an integer exit code
