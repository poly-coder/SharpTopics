module SharpTopics.MongoImpl.SnapshotStore
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver
open MongoDB.Driver.Core
open SharpTopics.Core
open System.Threading

type Options = {
    conn: IMongoDatabase
    collection: string
}

type CreateResult = {
    terminate: unit -> unit
    store: ITopicSnapshotStore
}

type BsonTopicSnapshot = {
    [<BsonId()>]
    topicId: string
    [<BsonElement("state")>]
    topicState: BsonTopicState
}
and BsonTopicState = {
    [<BsonElement("nextSeq")>]
    nextSequence: uint64
    [<BsonElement("lastTS")>]
    lastTimestamp: uint64 option
}

type internal Command =
| GetSnapshot of TopicKey * AsyncReplyChannel<Result<TopicSnapshot, exn>>
| SaveSnapshot of TopicSnapshot * AsyncReplyChannel<Result<unit, exn>>

let createStore (opts: Options) =
    let cts = new CancellationTokenSource()
    let terminate () = cts.Cancel()
    let collection = opts.conn.GetCollection<BsonTopicSnapshot>(opts.collection)

    let processor (mb: MailboxProcessor<_>) =
        let rec loop () = async {
            match! mb.Receive() with
            | GetSnapshot (topicId, reply) ->
                
                do()

            | SaveSnapshot (snapshot, reply) ->
                do()

            return! loop()
        }
        loop ()

    let mailbox = MailboxProcessor.Start(processor, cts.Token)

    let getSnapshot topicId =
        mailbox.PostAndAsyncReply(fun reply -> GetSnapshot(topicId, reply))

    let saveSnapshot snapshot =
        mailbox.PostAndAsyncReply(fun reply -> SaveSnapshot(snapshot, reply))

    let store =
        { new ITopicSnapshotStore with
            member __.getSnapshot topicId = getSnapshot topicId
            member __.saveSnapshot snapshot = saveSnapshot snapshot }
    
    { 
        terminate = terminate
        store = store
    }
