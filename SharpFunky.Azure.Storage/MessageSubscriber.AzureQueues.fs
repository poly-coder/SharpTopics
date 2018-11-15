module SharpFunky.Messaging.MessageSubscriber.AzureQueues

open SharpFunky
open SharpFunky.Conversion
open SharpFunky.Messaging
open Microsoft.WindowsAzure.Storage.Queue

module AsStringContent =

    type Options<'a> = {
        queue: CloudQueue
        converter: IAsyncConverter<string, 'a>
    }

    [<RequireQualifiedAccess>]
    module Options =
        let from queue converter = 
            {
                queue = queue
                converter = converter
            }

    let fromOptions opts =
    
        let subscribe message =
            let! data = opts.converter.convert message
            let msg = CloudQueueMessage(data)
            do! opts.queue.AddMessageAsync(msg) |> AsyncResult.ofTaskVoid

        MessagePublisher.createInstance subscribe
