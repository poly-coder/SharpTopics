module SharpFunky.Messaging.MessageSubscriber.AzureQueues

open SharpFunky
open SharpFunky.Conversion
open SharpFunky.Messaging
open Microsoft.WindowsAzure.Storage.Queue

module AsStringContent =
    open System.Collections.Generic
    open System.Threading

    type Options<'a> = {
        queue: CloudQueue
        converter: IAsyncConverter<string, 'a>
        batchSize: int
        progressiveDelay: int seq
        abandonOnError: bool
        onError: AsyncFn<exn, unit>
    }

    [<RequireQualifiedAccess>]
    module Options =
        let from queue converter = 
            {
                queue = queue
                converter = converter
                batchSize = 32
                progressiveDelay = [ 100; 200; 500; 1_000; 5_000; 20_000; 60_000 ]
                abandonOnError = false
                onError = AsyncFn.return' ()
            }
        let withBatchSize value opts = { opts with batchSize = value }
        let withProgressiveDelay value opts = { opts with progressiveDelay = value }
        let withOnError value = fun opts -> { opts with onError = value }
        let withAbandonOnError value = fun opts -> { opts with abandonOnError = value }

    let minDelay = 10
    let defaultDelay = 1000

    let fromOptions opts =
        let subscribe handler =
            let delay = ref None
            let delays = ref None
            let cancellation = new CancellationTokenSource()
            let token = cancellation.Token
            
            let waitIfNeeded() = async {
                match !delay with
                | None | Some 0 -> ()
                | Some d ->
                    do! Async.Sleep <| max d minDelay
                    match !delays with
                    | None -> ()
                    | Some ds ->
                        if (ds: IEnumerator<_>).MoveNext() then
                            delay := Some ds.Current
                        else
                            delays := None
            }

            let onEmptyBatch() = async {
                match !delays with
                | Some ds ->
                    if (ds: IEnumerator<_>).MoveNext() |> not then
                        disposeOf ds
                        delays := None
                | None ->
                    let ds = opts.progressiveDelay.GetEnumerator()
                    if ds.MoveNext() then
                        delays := Some ds
                    else
                        delay := Some defaultDelay

                match !delays with
                | Some ds ->
                    delay := Some ds.Current
                | None -> ()
            }

            let onFullBatch() = async {
                match !delays with Some ds -> disposeOf ds | _ -> ()
                delay := None
                delays := None
            }

            let processMessage msg = async {
                try
                    let text = (msg: CloudQueueMessage).AsString
                    let! messageResult = opts.converter.convert text

                    match messageResult with
                    | Ok message -> 
                        do! handler message
                        do! opts.queue.DeleteMessageAsync(msg) 
                            |> Async.ofTaskVoid
                            |> Async.ignoreExn
                        
                    | Error exn ->
                        if opts.abandonOnError then
                            do! opts.queue.DeleteMessageAsync(msg) 
                                |> Async.ofTaskVoid
                                |> Async.ignoreExn
                        do! opts.onError exn

                with exn ->
                    if opts.abandonOnError then
                        do! opts.queue.DeleteMessageAsync(msg) 
                            |> Async.ofTaskVoid
                            |> Async.ignoreExn
                    do! opts.onError exn
                        |> Async.ignoreExn
            }

            let rec loop () = async {
                if token.IsCancellationRequested then
                    return ()

                do! waitIfNeeded() |> Async.ignoreExn

                // After possibly waiting, get a batch of messages and handle them
                let! batch =
                    opts.queue.GetMessagesAsync(opts.batchSize)
                    |> Async.ofTask
                    |> Async.map Seq.toList

                match batch with
                | [] ->
                    do! onEmptyBatch() |> Async.ignoreExn

                | _ ->
                    do! onFullBatch() |> Async.ignoreExn
                    for msg in batch do
                        if token.IsCancellationRequested then
                            return ()
                        do! processMessage msg
                
                return! loop()
            }

            Async.StartAsTask(loop(), cancellationToken = token) |> ignore
            Disposable.createInstance (fun() -> cancellation.Cancel())

        MessageSubscriber.createInstance subscribe
