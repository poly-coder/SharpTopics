module SharpTopics.Core.TopicActor

open Akka.FSharp
open System

type State = {
    messageCount: uint64
    nextSequence: TopicSequence
    lastTimestamp: TopicTimestamp option
}

type GetStatusResponse =
| GetStatusResponse of State

type Command =
| GetStatus
| AppendMessage of TopicMessage

type internal InternalCommand =
| InternalInitialize of State
| InternalInitTimeout of TimeSpan

type Options = {
    factory: ITopicStorageFactory
    topicKey: TopicKey
    initTimeout: TimeSpan option
}

let internal build (st: ITopicStorage) =
    fun (mb: Actor<obj>) -> 
        let rec loop state = actor {
            let! msg = mb.Receive()

            let state' =
                match msg with
                | :? Command as cmd ->
                    match state, cmd with
                    | None, _ ->
                        do mb.Stash()
                        None

                    | Some state, GetStatus ->
                        do mb.Sender() <! GetStatusResponse state
                        Some state

                    | Some state, AppendMessage message ->
                        // TODO: implement
                        raise <| NotImplementedException()

                | :? InternalCommand as cmd ->
                    match state, cmd with
                    | None, InternalInitialize state ->
                        Some state

                    | None, InternalInitTimeout timeout ->
                        do mb.Context.SetReceiveTimeout(Nullable timeout)
                        None

                    | _, InternalInitTimeout timeout ->
                        state

                    | _, InternalInitialize _ ->
                        mb.Unhandled(cmd)
                        state

                | _ ->
                    mb.Unhandled(msg)
                    state

            return! loop state
        }
        loop None

let spawnTopic options system name =
    let st = options.factory.create options.topicKey
    let actorRef = spawn system name (build st)

    match options.initTimeout with
    | Some t ->
        do actorRef <! InternalInitTimeout t
    | None -> do()

    async {
        let! state = st.getState()
        let cmd: State = {
            messageCount = state.messageCount
            nextSequence = state.nextSequence
            lastTimestamp = state.lastTimestamp
        }
        do actorRef <! InternalInitialize cmd
    }
    |> Async.StartAsTask
    |> ignore

    actorRef