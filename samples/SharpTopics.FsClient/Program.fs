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

let makeMsg id text =
    Message.empty
        |> OptLens.setSome Message.messageId id
        |> OptLens.setSome Message.contentType "text/plain"
        |> OptLens.setSome Message.data (String.toUtf8 text)

let worker (client: IClusterClient) = task {
    let friend = client.GetGrain<IMessagePublisher> "12345"
    let messages = [
        makeMsg "message0003" "This is just a basic test, you know!!!"
    ]
    let! response = friend.PublishMessages messages
    printfn "%A" (response |> Seq.toList)
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
