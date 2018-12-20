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
open System.Threading
open System.Text.RegularExpressions

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
        //.ConfigureLogging(fun logging ->
        //    logging.AddConsole() |> ignore
        //)
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
            let publisher = client.GetGrain<IMessagePublisher> (sprintf "topic%i" index)
            return! publisher.PublishMessages messages
        }]
    do watch.Stop()
    do printfn "LOOPS: %d, COUNT: %d, SIZE: %d" loops count size
    do printfn "ELAPSED TIME: %dms" watch.ElapsedMilliseconds
    do printfn "ELAPSED TIME PER OPERATION: %.3fms" ((float watch.ElapsedMilliseconds) / (float loops))
}

let quickTest argv =
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

let fromGroup tryParser name def (match': Match) =
    match'.Groups.[name: string]
    |> fun g -> if g.Success then Some g.Value else None
    |> Option.bind tryParser
    |> Option.defaultValue def
let fromGroupInt32 = fromGroup Int32.tryParse

let (|ExitCommand|_|) line =
    if Regex.IsMatch(line, @"^exit\s*$", RegexOptions.IgnoreCase) then Some() else None

let (|SendTextCommand|_|) line =
    let m = Regex.Match(line, @"^send\s+(?<p>[A-Z0-9]{3,63})\s+((?<c>[\d]+)\s*\*\s*)?(?<m>.*)\s*$", RegexOptions.IgnoreCase)
    if m.Success then
        let topicName =
            m.Groups.["p"].Value
        let count = fromGroupInt32 "c" 1 m
        let text =
            m.Groups.["m"].Value
        Some (topicName, count, text)
    else None

let (|ChunkTextCommand|_|) line =
    let m = Regex.Match(line, @"^chunk\s+(?<p>[A-Z0-9]{3,63})\s+(?<i>[\d]+)\s+(?<f>[\d]+)\s+(?<t>[\d]+)\s*$", RegexOptions.IgnoreCase)
    if m.Success then
        let topicName =
            m.Groups.["p"].Value
        let index = fromGroupInt32 "i" 0 m
        let from = fromGroupInt32 "f" 0 m
        let to' = fromGroupInt32 "t" 0 m
        Some (topicName, index, from, to')
    else None

let (|ReadTextCommand|_|) line =
    let m = Regex.Match(line, @"^read\s+(?<p>[A-Z0-9]{3,63})(\s+(?<c>[\d]+))?\s*$", RegexOptions.IgnoreCase)
    if m.Success then
        let topicName =
            m.Groups.["p"].Value
        let count =
            m.Groups.["c"]
            |> fun g -> if g.Success then g.Value else "-1"
            |> Int32.tryParse
            |> Option.defaultValue -1
        Some (topicName, count)
    else None

let runCommand (prepare: unit -> Task<_>) (run: _ -> Task<_>) = task {
    try
        let! data = prepare()
        let watch = Stopwatch.StartNew()
        let! result = run data
        do watch.Stop()
        printfn "DONE: %.3fms" watch.Elapsed.TotalMilliseconds
        if not <| obj.ReferenceEquals((), result) then
            printfn "    RESULT: %A" result
    with exn ->
        printfn "ERROR: %s" exn.Message
}

let replTest() =
    let tokenSource = new CancellationTokenSource()
    let token = tokenSource.Token

    let mutable cancelSub: IDisposable = null
    cancelSub <- Console.CancelKeyPress.Subscribe(fun e -> 
        cancelSub.Dispose()
        do e.Cancel <- true
        do tokenSource.Cancel()
    )

    let rec replLoop (client: IClusterClient) = task {
        let closeClient() =
            do printf "Disconnecting "
            do client.Dispose()
            do printfn "DONE!"

        printf ">>> "
        token.ThrowIfCancellationRequested()
        let line = Console.ReadLine()
        if isNull line then
            tokenSource.Cancel()
            token.ThrowIfCancellationRequested()

        match line with
        | null 
        | ExitCommand ->
            closeClient()
            return ()

        | SendTextCommand (topicName, count, text) ->
            do printf "    Sending %i messages to %s ... " count topicName
            do! runCommand
                    (fun () -> task {
                        let publisher = client.GetGrain<IMessagePublisher> topicName
                        let messages =
                            if count > 1 then
                                [ for index in 1 .. count ->
                                    makeMsg (sprintf "%s (#%i)" text index) ]
                            else
                                [ makeMsg text ]
                        return publisher, messages
                    })
                    (fun (publisher, messages) -> publisher.PublishMessages messages)

        | ChunkTextCommand (topicName, index, from, to') ->
            do printf "    Reading chunk %i of %s from %i to %i ... " index topicName from to'
            do! runCommand
                    (fun () -> task {
                        let chunk = client.GetGrain<IMessageChunk>(int64 index, topicName, null)
                        return chunk
                    })
                    (fun chunk -> chunk.FromSequenceRange(int64 from, int64 to'))

        | ReadTextCommand (topicName, count) ->
            do printf "    /// Reading %i messages from %s ... " count topicName
            
            //let rec readLoop count = task {
            //    if count = 0 then
            //}
            //return! readLoop count

        | _ ->
            do printfn "Unknown command"
        
        return! replLoop client
    }

    let replTask = task {
        let client = buildClient()
        do printf "Connecting "
        do! client.Connect(fun exn -> task {
            do printf "."
            token.ThrowIfCancellationRequested()
            do! Task.Delay(1000)
            return true
        })
        token.ThrowIfCancellationRequested()
        do printfn " DONE!"
        return! replLoop client
    }

    try replTask.Wait()
    with 
    | :? OperationCanceledException -> do printfn " CANCELLED!"
    | exn -> do printfn "%s: %s" (exn.GetType().Name) exn.Message

[<EntryPoint>]
let main argv =
    replTest()

    if Debugger.IsAttached then
        Console.WriteLine("Press any key to continue ...")
        Console.ReadKey() |> ignore

    0 // return an integer exit code
