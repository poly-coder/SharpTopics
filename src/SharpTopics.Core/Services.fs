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

type IMessageStore =
    abstract fetchStatus: unit -> Task<MessageStoreStatus>
    abstract storeMessagesAndStatus: Message list -> MessageStoreStatus -> Task<unit>
