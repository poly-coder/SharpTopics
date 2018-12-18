namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans
open SharpTopics.Core

type IMessagePublisherSubscriber =
    inherit IGrainObserver

    abstract MessagesPublished: empty: unit -> unit

type IMessagePublisher =
    inherit IGrainWithStringKey

    abstract PublishMessages: messages: Message list -> Task<MessageMeta list>

    abstract Subscribe: observer: IMessagePublisherSubscriber -> Task<unit>
    abstract Unsubscribe: observer: IMessagePublisherSubscriber -> Task<unit>



type ChunkInfo = {
    minSequence: int64
    maxSequence: int64
    nextSequence: int64
    isComplete: bool
} with
    static member empty() = {
        minSequence = 0L
        maxSequence = 0L
        nextSequence = 0L
        isComplete = false
    }

type MessageListResult = {
    messages: Message list
}

type IMessageStoreChunk =
    inherit IGrainWithIntegerCompoundKey

    abstract GetChunkInfo: empty: unit -> Task<ChunkInfo>

    abstract FromSequenceRange: fromSeq: int64 * toSeq: int64 -> Task<MessageListResult>
