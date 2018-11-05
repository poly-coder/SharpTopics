module SharpTopics.Core.TopicActor

open Akka.FSharp

type State = {
    messageCount: uint64
    nextSequence: TopicSequence
    lastTimestamp: TopicTimestamp option
}

type GetStatusResponse =
| GetStatusResponse of State

type Command =
| GetStatus
| InternalInitialize of State

type Options = {
    factory: ITopicStorageFactory
    topicKey: TopicKey
}

let internal build (st: ITopicStorage) =
    fun (mb: Actor<_>) -> 
        let rec loop state = actor {
            let! msg = mb.Receive()

            let state' =
                match state, msg with
                | None, InternalInitialize state ->
                    do mb.UnstashAll()
                    Some state

                | Some state, GetStatus ->
                    do mb.Sender() <! GetStatusResponse state
                    Some state

                | None, _ ->
                    do mb.Stash()
                    None

                | _, InternalInitialize state ->
                    do mb.Unhandled(InternalInitialize state)
                    state


            return! loop state'
        }
        loop None

let spawnTopic options system name =
    let st = options.factory.create options.topicKey
    let actorRef = spawn system name
    async {
        let! state = st.getState()
        let cmd: State = {
            messageCount = state.messageCount
            nextSequence = state.nextSequence
            lastTimestamp = state.lastTimestamp
        }
        actorRef <! InternalInitialize cmd
    } |> Async.StartAsTask
    actorRef