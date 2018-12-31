namespace SharpTopics.Core

open System.Threading.Tasks

type MessageStoreStatus = {
    isFrozen: bool
    nextSequence: int64
}

module MessageStoreStatus =
    let empty = {
        isFrozen = false
        nextSequence = 0L
    }

type FetchMessagesResult = {
    messages: Message seq
    nextSequence: int64
}

type IMessageStore =
    abstract fetchStatus: partition: string -> Task<MessageStoreStatus>
    
    abstract storeMessagesAndStatus: partition: string -> Message list -> MessageStoreStatus -> Task<unit>

    abstract fetchMessagesAndStatus: partition: string -> from: int64 -> toExclusive: int64 -> Task<FetchMessagesResult>
