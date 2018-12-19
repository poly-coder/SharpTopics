namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans
open SharpTopics.Core

// IMessagePublisher

type IMessagePublisherObserver =
    inherit IGrainObserver

    abstract MessagesPublished: empty: unit -> unit

type IMessagePublisher =
    inherit IGrainWithStringKey

    abstract PublishMessages: messages: Message list -> Task<MessageMeta list>

    abstract Subscribe: observer: IMessagePublisherObserver -> Task<unit>
    abstract Unsubscribe: observer: IMessagePublisherObserver -> Task<unit>

// IMessageStoreChunk

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

type IMessageStoreChunkObserver =
    inherit IGrainObserver

    abstract MessagesAvailable: empty: unit -> unit
    abstract MessagesComplete: empty: unit -> unit

type IMessageStoreChunk =
    inherit IGrainWithIntegerCompoundKey

    abstract GetChunkInfo: empty: unit -> Task<ChunkInfo>

    abstract FromSequenceRange: fromSeq: int64 * toSeq: int64 -> Task<MessageListResult>

    abstract Subscribe: observer: IMessageStoreChunkObserver -> Task<unit>
    abstract Unsubscribe: observer: IMessageStoreChunkObserver -> Task<unit>

// IMessageReader