module SharpFunky.Messaging.MessagePublisher.AzureQueues

open SharpFunky
open SharpFunky.Conversion
open SharpFunky.Messaging
open Microsoft.WindowsAzure.Storage.Queue

module AsStringContent =

    type Options<'a> = {
        queue: CloudQueue
        converter: IAsyncConverter<'a, string>
    }

    [<RequireQualifiedAccess>]
    module Options =
        let from queue converter = 
            {
                queue = queue
                converter = converter
            }

    let fromOptions opts =
    
        let publish message = asyncResult {
            let! data = opts.converter.convert message
            let msg = CloudQueueMessage(data)
            do! opts.queue.AddMessageAsync(msg) |> AsyncResult.ofTaskVoid
        }

        MessagePublisher.createInstance publish

module AsBinaryContent =

    type Options<'a> = {
        queue: CloudQueue
        converter: IAsyncConverter<'a, byte[]>
    }

    [<RequireQualifiedAccess>]
    module Options =
        let from queue converter = 
            {
                queue = queue
                converter = converter
            }

    let fromOptions opts =
    
        let publish message = asyncResult {
            let! data = opts.converter.convert message
            let msg = CloudQueueMessage.CreateCloudQueueMessageFromByteArray(data)
            do! opts.queue.AddMessageAsync(msg) |> AsyncResult.ofTaskVoid
        }

        MessagePublisher.createInstance publish

