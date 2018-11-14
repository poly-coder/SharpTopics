module SharpFunky.Messaging

open SharpFunky
open SharpFunky.Messaging
open SharpFunky.Conversion

module MessagePublisher =
    module NATS =
        open NATS.Client

        type Options<'t> = {
            subject: string
            connection: IConnection
            converter: IConverter<'t, byte[]>
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
            converter: IConverter<'t, byte[]>
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
            connection: IConnection
            converter: IConverter<'t, byte[]>
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
        open System

        type Options<'t> = {
            subject: string
            queue: string option
            connection: IStanConnection
            converter: IConverter<byte[], 't>
        }

        [<RequireQualifiedAccess>]
        module Options =
            let from subject queue connection converter = 
                {
                    subject = subject
                    queue = queue
                    connection = connection
                    converter = converter
                }

        let fromOptions opts =
            let subscribe handler =
                let handler' (_: obj) (args: StanMsgHandlerArgs) =
                    asyncResult {
                        let! msg = opts.converter.convert args.Message.Data
                        handler msg
                    }
                    |> AsyncResult.ignoreAll
                    |> Async.StartAsTask
                    |> ignore
                let sub =
                    match opts.queue with
                    | Some q -> opts.connection.Subscribe(opts.subject, q, handler')
                    | None -> opts.connection.Subscribe(opts.subject, EventHandler<StanMsgHandlerArgs> handler')
                { new IDisposable with
                    member __.Dispose() = sub.Unsubscribe() }
                
            MessageSubscriber.createInstance subscribe

