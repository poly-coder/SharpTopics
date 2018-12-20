namespace SharpTopics.FsInterfaces

open Orleans
open System.Threading.Tasks
open SharpTopics.Core

type MessageChunkResult = {
    messages: Message list
    chunkCouldReceiveMoreMessages: bool
    requestHasMoreMessagesInChunk: bool
}

type IMessageChunkObserver =
    inherit IGrainObserver

    abstract MessagesAvailable: empty: unit -> unit
    abstract MessagesComplete: empty: unit -> unit

type IMessageChunk =
    inherit IGrainWithIntegerCompoundKey

    abstract FromSequenceRange: fromSeq: int64 * toSeq: int64 -> Task<MessageChunkResult>

    abstract Subscribe: observer: IMessageChunkObserver -> Task<unit>
    abstract Unsubscribe: observer: IMessageChunkObserver -> Task<unit>
