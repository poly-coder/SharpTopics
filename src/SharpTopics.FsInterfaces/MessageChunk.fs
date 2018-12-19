namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans
open SharpTopics.Core

type MessageListResult = {
    messages: Message list
    isComplete: bool
}

type IMessageChunkObserver =
    inherit IGrainObserver

    abstract MessagesAvailable: empty: unit -> unit
    abstract MessagesComplete: empty: unit -> unit

type IMessageChunk =
    inherit IGrainWithIntegerCompoundKey

    abstract FromSequenceRange: fromSeq: int64 * toSeq: int64 -> Task<MessageListResult>

    abstract Subscribe: observer: IMessageChunkObserver -> Task<unit>
    abstract Unsubscribe: observer: IMessageChunkObserver -> Task<unit>
