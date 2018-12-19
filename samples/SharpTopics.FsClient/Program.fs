open Microsoft.Extensions.Logging
open Orleans
open Orleans.Runtime.Configuration
open Orleans.Hosting
open System
open FSharp.Control.Tasks.V2
open SharpTopics.FsInterfaces
open System.Threading.Tasks
open SharpTopics.Core
open SharpFunky
open System.Diagnostics

let buildClient() =
    let builder = new ClientBuilder()
    builder
        .UseLocalhostClustering()
        .ConfigureApplicationParts(fun parts ->
            parts
                .AddApplicationPart(typeof<IMessagePublisher>.Assembly)
                .WithCodeGeneration()
                |> ignore
        )
        .ConfigureLogging(fun logging ->
            logging.AddConsole() |> ignore
        )
        .Build()

let makeMsg text =
    Message.empty
        // |> OptLens.setSome Message.messageId id
        |> OptLens.setSome Message.contentType "text/plain"
        |> OptLens.setSome Message.data (String.toUtf8 text)

let worker loops count size (client: IClusterClient) = task {
    let publisher = client.GetGrain<IMessagePublisher> "12345"
    let msg = "This is just a basic test, you know!!!"
    let msg = String.replicate (max 1 (size / msg.Length)) msg
    let messages = [ for _ in 1 .. count -> makeMsg msg ]
    let watch = Stopwatch.StartNew()
    let! _ = Task.WhenAll [ for _ in 1 .. loops -> publisher.PublishMessages messages ]
    do watch.Stop()
    do printfn "LOOPS: %d, COUNT: %d, SIZE: %d" loops count size
    do printfn "ELAPSED TIME: %dms" watch.ElapsedMilliseconds
    do printfn "ELAPSED TIME PER OPERATION: %.3fms" ((float watch.ElapsedMilliseconds) / (float loops))
}

let workerPartitioned loops count size (client: IClusterClient) = task {
    let msg = "This is just a basic test, you know!!!"
    let msg = String.replicate (max 1 (size / msg.Length)) msg
    let messages = [ for _ in 1 .. count -> makeMsg msg ]
    let watch = Stopwatch.StartNew()
    let! _ = Task.WhenAll [ 
        for index in 1 .. loops -> task {
            let publisher = client.GetGrain<IMessagePublisher> (sprintf "id%i" index)
            return! publisher.PublishMessages messages
        }]
    do watch.Stop()
    do printfn "LOOPS: %d, COUNT: %d, SIZE: %d" loops count size
    do printfn "ELAPSED TIME: %dms" watch.ElapsedMilliseconds
    do printfn "ELAPSED TIME PER OPERATION: %.3fms" ((float watch.ElapsedMilliseconds) / (float loops))
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
        do! workerPartitioned 1000 1 1024 client
        do! workerPartitioned 1000 1 1024 client
        do! workerPartitioned 1000 1 1024 client
    }

    t.Wait()

    Console.ReadKey() |> ignore

    0 // return an integer exit code
