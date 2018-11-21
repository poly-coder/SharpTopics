module SharpFunky.Messaging.MessageSubscriber.AzureServiceBus

open SharpFunky
open SharpFunky.Conversion
open SharpFunky.Messaging
open Microsoft.Azure.ServiceBus
open System

type Options<'a> = {
    queue: IQueueClient
    converter: IAsyncConverter<(Map<string, obj> * byte[]), 'a>
    onError: AsyncFn<exn, unit>
    abandonOnError: bool
}

[<RequireQualifiedAccess>]
module Options =
    let from queue converter = 
        {
            queue = queue
            converter = converter
            onError = AsyncFn.return' ()
            abandonOnError = false
        }
    let withOnError value = fun opts -> { opts with onError = value }
    let withAbandonOnError value = fun opts -> { opts with abandonOnError = value }

let fromOptions opts =
    
    let subscribe handler = 
        let handler' (msg: Message) _ =
            async {
                let meta =
                    msg.UserProperties
                    |> Seq.map (fun p -> p.Key, p.Value)
                    |> Map.ofSeq
                let! convertResult = opts.converter.convert(meta, msg.Body)
                match convertResult with
                | Ok value ->
                    do! handler value
                    do! opts.queue.CompleteAsync(msg.SystemProperties.LockToken) |> Async.ofTaskVoid
                | Error exn ->
                    if opts.abandonOnError then
                        do! opts.queue.AbandonAsync(msg.SystemProperties.LockToken) |> Async.ofTaskVoid
                    raise exn
            } 
            |> Async.startAsTaskVoid
        let exnHandler' (args: ExceptionReceivedEventArgs) = 
            opts.onError args.Exception
            |> Async.startAsTaskVoid
        opts.queue.RegisterMessageHandler(
            Func<_, _, _> handler',
            Func<_, _> exnHandler')
        Disposable.noop

    MessageSubscriber.createInstance subscribe
