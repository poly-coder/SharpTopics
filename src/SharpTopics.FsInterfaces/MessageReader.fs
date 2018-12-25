namespace SharpTopics.FsInterfaces

open Orleans
open System.Threading.Tasks
open SharpTopics.Core
open SharpFunky

type MessageListResult = {
    messages: Message list
    endOfCurrentTopic: bool
}

type ReadFrom =
    | ReadFromStart
    | ReadFromSequence of int64

type MessageReaderInit = {
    readFrom: ReadFrom
    initialQuota: int64
    topicId: string
}

module MessageReaderInit =

    let create topicId = {
        readFrom = ReadFromStart
        initialQuota = 0L
        topicId = topicId
    }
    let topicId = Lens.cons' (fun e -> e.topicId) (fun v e -> { e with topicId = v })
    let readFrom = Lens.cons' (fun e -> e.readFrom) (fun v e -> { e with readFrom = v })
    let initialQuota = Lens.cons' (fun e -> e.initialQuota) (fun v e -> { e with initialQuota = v })

type IMessageReaderObserver =
    inherit IGrainObserver

    abstract AcceptMessages: messages: MessageListResult -> unit

type IMessageReader =
    inherit IGrainWithGuidKey

    abstract Initialize: init: MessageReaderInit -> Task<unit>
    abstract IssueQuota: count: int -> Task<unit>

    abstract Subscribe: observer: IMessageReaderObserver -> Task<unit>
    abstract Unsubscribe: observer: IMessageReaderObserver -> Task<unit>
