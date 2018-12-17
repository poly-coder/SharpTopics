﻿namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans
open SharpTopics.Core

type IMessagePublisherSubscriber =
    inherit IGrainObserver

    abstract MessagesPublished: unit -> Task<unit>

type IMessagePublisher =
    inherit IGrainWithStringKey

    abstract PublishMessages: Message seq -> Task<MessageMeta seq>

    abstract Subscribe: IMessagePublisherSubscriber -> Task<unit>
    abstract Unsubscribe: IMessagePublisherSubscriber -> Task<unit>



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

    abstract GetChunkInfo: unit -> Task<ChunkInfo>

    abstract FromSequenceRange: fromSeq: int64 -> toSeq: int64 -> Task<MessageListResult>
