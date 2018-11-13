namespace SharpTopics.Core

open SharpFunky

type TopicKey = string
type TopicSequence = uint64
type TopicTimestamp = uint64

type TopicState = {
    nextSequence: TopicSequence
    lastTimestamp: TopicTimestamp option
}

type PublishResult = {
    sequence: TopicSequence
    timestamp: TopicTimestamp
    topicState: TopicState
}

type ITopicMessagePublisher =
    abstract publish: TopicMessage -> Async<Result<PublishResult, exn>>

type TopicSnapshot = {
    topicId: TopicKey
    topicState: TopicState
}

type ITopicSnapshotStore =
    abstract getSnapshot: TopicKey -> AsyncResult<TopicSnapshot, exn>
    abstract saveSnapshot: TopicSnapshot -> AsyncResult<unit, exn>

//module TopicSnapshotStore =
//    let 

//type ITopicStorage =
//    abstract getState: unit -> Async<TopicState>

//type ITopicStorageFactory =
//    abstract create: TopicKey -> ITopicStorage
