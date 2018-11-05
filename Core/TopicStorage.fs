namespace SharpTopics.Core

type TopicKey = string
type TopicSequence = uint64
type TopicTimestamp = uint64

type TopicState = {
    messageCount: uint64
    nextSequence: TopicSequence
    lastTimestamp: TopicTimestamp option
}

type ITopicStorage =
    abstract getState: unit -> Async<TopicState>

type ITopicStorageFactory =
    abstract create: TopicKey -> ITopicStorage

