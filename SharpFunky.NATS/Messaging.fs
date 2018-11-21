module SharpFunky.Messaging

open System
open SharpFunky
open SharpFunky.Messaging
open SharpFunky.Conversion

module MessagePublisher =
    module NATS =
        open NATS.Client

        type Options<'t> = {
            subject: string
            connection: IConnection
            converter: IAsyncConverter<'t, byte[]>
        }

        [<RequireQualifiedAccess>]
        module Options =
            let from subject connection converter = 
                {
                    subject = subject
                    connection = connection
                    converter = converter
                }

        let fromOptions opts =
            let publish message = asyncResult {
                let! bytes = opts.converter.convert message
                opts.connection.Publish(opts.subject, bytes)
                return ()
            }
            MessagePublisher.createInstance publish

    module STAN =
        open STAN.Client

        type Options<'t> = {
            subject: string
            connection: IStanConnection
            converter: IAsyncConverter<'t, byte[]>
        }

        [<RequireQualifiedAccess>]
        module Options =
            let from subject connection converter = 
                {
                    subject = subject
                    connection = connection
                    converter = converter
                }

        let fromOptions opts =
            let publish message = asyncResult {
                let! bytes = opts.converter.convert message
                let! response = opts.connection.PublishAsync(opts.subject, bytes) |> Async.mapTask Result.ok
                return response
            }
            MessagePublisher.createInstance publish


module MessageSubscriber =
    module NATS =
        open NATS.Client

        type Options<'t> = {
            subject: string
            queue: string option
            connection: IConnection
            converter: IAsyncConverter<byte[], 't>
            messageLimit: int64
            bytesLimit: int64
            autoUnsubscribe: int option
            onError: AsyncFn<exn, unit>
        }

        [<RequireQualifiedAccess>]
        module Options =
            let from subject connection converter = 
                {
                    subject = subject
                    queue = None
                    connection = connection
                    converter = converter
                    messageLimit = 1L <<< 10
                    bytesLimit = 10L <<< 20
                    autoUnsubscribe = None
                    onError = AsyncFn.return' ()
                }

            let withQueue queue o = { o with queue = Some queue }
            let withoutQueue o = { o with queue = None }
            let withMessageLimit messageLimit o = { o with messageLimit = messageLimit }
            let withBytesLimit bytesLimit o = { o with bytesLimit = bytesLimit }
            let withAutoUnsubscribe max o = { o with autoUnsubscribe = Some max }
            let withOnError value = fun opts -> { opts with onError = value }

        let fromOptions opts =
            let subscribe handler =
                let handler' (_: obj) (args: MsgHandlerEventArgs) =
                    async {
                        let! msgResult = opts.converter.convert args.Message.Data
                        match msgResult with
                        | Ok msg ->
                            do! handler msg
                        | Error exn ->
                            do! opts.onError exn
                    }
                    |> Async.ignoreExn
                    |> Async.StartAsTask
                    |> ignore

                let sub =
                    match opts.queue with
                    | Some q ->
                        opts.connection.SubscribeAsync(
                            opts.subject, q, 
                            EventHandler<MsgHandlerEventArgs> handler')
                    | None ->
                        opts.connection.SubscribeAsync(
                            opts.subject, 
                            EventHandler<MsgHandlerEventArgs> handler')

                do sub.SetPendingLimits(opts.messageLimit, opts.bytesLimit)
                match opts.autoUnsubscribe with
                | Some v -> do sub.AutoUnsubscribe(v)
                | None _ -> do ()

                do sub.Start()
                { new IDisposable with
                    member __.Dispose() = sub.Unsubscribe() }
                
            MessageSubscriber.createInstance subscribe

    module STAN =
        open STAN.Client

        type Options<'t> = {
            subject: string
            queue: string option
            connection: IStanConnection
            converter: IAsyncConverter<byte[], 't>
            onError: AsyncFn<exn, unit>
        }

        [<RequireQualifiedAccess>]
        module Options =
            let from subject connection converter = 
                {
                    subject = subject
                    queue = None
                    connection = connection
                    converter = converter
                    onError = AsyncFn.return' ()
                }

            let withQueue queue o = { o with queue = Some queue }
            let withoutQueue o = { o with queue = None }
            let withOnError value = fun opts -> { opts with onError = value }

        let fromOptions opts =
            let subscribe handler =
                let handler' (_: obj) (args: StanMsgHandlerArgs) =
                    async {
                        let! msgResult = opts.converter.convert args.Message.Data
                        match msgResult with
                        | Ok msg ->
                            do! handler msg
                            do args.Message.Ack()
                        | Error exn ->
                            do! opts.onError exn
                    }
                    |> Async.ignoreExn
                    |> Async.StartAsTask
                    |> ignore

                let sub =
                    match opts.queue with
                    | Some q ->
                        opts.connection.Subscribe(
                            opts.subject, q, 
                            EventHandler<StanMsgHandlerArgs> handler')
                    | None ->
                        opts.connection.Subscribe(
                            opts.subject,
                            EventHandler<StanMsgHandlerArgs> handler')

                { new IDisposable with
                    member __.Dispose() = sub.Unsubscribe() }
                
            MessageSubscriber.createInstance subscribe
