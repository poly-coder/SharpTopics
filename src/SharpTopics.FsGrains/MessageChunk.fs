namespace SharpTopics.FsGrains

open Orleans
open System
open SharpTopics.Core
open SharpTopics.FsInterfaces
open FSharp.Control.Tasks.V2
open System.Threading.Tasks
open SharpFunky
open System.Collections.Generic

type IMessageChunkRefresh =
    inherit IGrainWithIntegerCompoundKey

    abstract RefreshChunk: unit -> Task<unit>

type MessageReaderOptions() =
    member val chunkSize = 1000L with get, set
    member val timerPeriod = 100.0 with get, set

type internal MessageChunkState = {
    partition: string
    minSequence: int64
    maxSequence: int64
    nextSequence: int64
    index: int64
    publisher: IMessagePublisher option
}

type MessageChunkGrain(store: IMessageStore, readerOptions: MessageReaderOptions) =
    inherit Grain()

    let state = ref {
        partition = ""
        index = -1L
        minSequence = -1L
        maxSequence = -1L
        nextSequence = -1L
        publisher = None
    }
    let subs = ObserverSubscriptionManager<IMessageChunkObserver>()
    let loadedMessages = List()
    let chunkHaveAllMessages() =
        !state
        |> fun st -> st.maxSequence = st.nextSequence
    let chunkCouldReceiveMoreMessages() =
        not(chunkHaveAllMessages()) &&
        Option.isSome state.Value.publisher

    override this.OnActivateAsync() =
        task {
            let mutable partition = ""
            let index = this.GetPrimaryKeyLong(&partition)
            let minSequence = index * readerOptions.chunkSize
            state := 
                {
                    !state with
                        partition = partition
                        index = index
                        minSequence = minSequence
                        maxSequence = minSequence + readerOptions.chunkSize
                        nextSequence = minSequence
                }
            do! this.RefreshChunkImpl()
        } :> Task

    member this.EnsurePublisher() =
        let factory = this.GrainFactory
        task {
            match state.Value.publisher with
            | None ->
                let pub = factory.GetGrain<IMessagePublisher>(state.Value.partition)
                state := { !state with publisher = Some pub }
                do! pub.Subscribe this
                return! this.RefreshChunkImpl()
            | Some _ ->
                return ()
        }

    member this.ReleasePublisher() = task {
        match state.Value.publisher with
        | Some pub ->
            do! pub.Unsubscribe this
            state := { !state with publisher = None }
        | None -> ()
        return ()
    }

    member this.RefreshChunkImpl() =
        task {
            if chunkHaveAllMessages() then return ()
            let! result =
                store.fetchMessagesAndStatus 
                    state.Value.partition
                    state.Value.nextSequence
                    state.Value.maxSequence

            state := { !state with nextSequence = result.nextSequence }
            loadedMessages.AddRange result.messages
            if not <| chunkHaveAllMessages() then
                do! this.EnsurePublisher()
            else
                do! this.ReleasePublisher()
        }

    member this.FromSequenceRangeImpl fromSeq toSeq = task {
        let inRange msg =
            match OptLens.getOpt Message.sequence msg with
            | Some s -> fromSeq <= s && s < toSeq
            | None -> false
        let messages =
            loadedMessages
            |> Seq.filter inRange
            |> List.ofSeq
        let requestHasMoreMessagesInChunk =
            messages
            |> List.tryLast
            |> Option.bind (OptLens.getOpt Message.sequence)
            |> Option.map (fun s -> s + 1L < min toSeq state.Value.maxSequence)
            |> Option.defaultValue true
        let result: MessageChunkResult = {
            chunkCouldReceiveMoreMessages = chunkCouldReceiveMoreMessages()
            requestHasMoreMessagesInChunk = requestHasMoreMessagesInChunk 
            messages = messages
        }
        return result
    }

    member this.SubscribeImpl observer = task {
        if subs.IsSubscribed observer |> not then
            subs.Subscribe observer
    }

    member this.UnsubscribeImpl observer = task {
        if subs.IsSubscribed observer then
            subs.Unsubscribe observer
    }

    interface IMessageChunkRefresh with
        member this.RefreshChunk() =
            this.RefreshChunkImpl()

    interface IMessageChunk with
        member this.FromSequenceRange(fromSeq, toSeq) =
            this.FromSequenceRangeImpl fromSeq toSeq

        member this.Subscribe observer =
            this.SubscribeImpl observer

        member this.Unsubscribe observer =
            this.UnsubscribeImpl observer

    interface IMessagePublisherObserver with
        member this.MessagesPublished() =
            let mutable partition = ""
            let index = this.GetPrimaryKeyLong(&partition)
            let self = this.GrainFactory.GetGrain<IMessageChunkRefresh>(index, partition, null)
            // TODO: Do this work? It makes me uncomfortable the ignore!!!
            self.RefreshChunk()
            |> ignore
