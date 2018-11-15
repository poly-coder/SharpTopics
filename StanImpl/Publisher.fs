module SharpTopics.StanImpl.Publisher
open STAN.Client
open SharpTopics.Core
open System.Threading

type Options = {
    conn: IStanConnection
    serializer: ITopicMessageSerializer
    subject: string
    topicState: TopicState
    clock: unit -> uint64
    genMessageId: unit -> string
    publishTimeout: int option
}

type CreateResult = {
    terminate: unit -> unit
    publisher: ITopicMessagePublisher
}

type internal Command =
| PublishMessage of TopicMessage * AsyncReplyChannel<Result<PublishResult, exn>>

let createPublisher (opts: Options) =
    let cts = new CancellationTokenSource()
    let terminate () = cts.Cancel()
    let serializeWith sequence timestamp =
        TopicMessage.setSequence sequence
        >> TopicMessage.setTimestamp timestamp
        >> TopicMessage.updMessageIdOpt (function None -> opts.genMessageId() |> Some | x -> x)
        >> opts.serializer.serialize

    let processor (mb: MailboxProcessor<_>) =
        let rec loop topicState = async {
            match! mb.Receive() with
            | PublishMessage (msg, reply) ->
                try
                    let sequence = topicState.nextSequence
                    let timestamp = opts.clock()
                    let! bytes = msg |> serializeWith sequence timestamp
                    let! ack = opts.conn.PublishAsync(opts.subject, bytes) |> Async.AwaitTask
                    do ignore ack
                    let topicState' = { 
                        topicState with 
                            nextSequence = topicState.nextSequence + 1UL
                            lastTimestamp = Some timestamp
                    }
                    let result = {
                        sequence = sequence
                        timestamp = timestamp
                        topicState = topicState'
                    }
                    do Ok result |> reply.Reply
                    return! loop topicState'
                with exn ->
                    do Error exn |> reply.Reply
                    return! loop topicState
        }
        loop opts.topicState

    let mailbox = MailboxProcessor.Start(processor, cts.Token)

    let publish msg =
        mailbox.PostAndAsyncReply(fun reply -> PublishMessage(msg, reply))

    let publisher =
        { new ITopicMessagePublisher with
            member __.publish msg = publish msg }
    
    { 
        terminate = terminate
        publisher = publisher
    }

