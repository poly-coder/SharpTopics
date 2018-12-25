namespace SharpTopics.FsGrains

open Orleans
open System
open SharpTopics.Core
open SharpTopics.FsInterfaces
open FSharp.Control.Tasks.V2
open System.Threading.Tasks
open SharpFunky
open System.Collections.Generic
open System.Threading

type IMessageReaderRefresh =
    inherit IGrainWithGuidKey

    abstract RefreshReader: available: bool -> Task<unit>

type internal MessageReaderState = {
    partition: string option
    quota: int64
    nextSequence: int64
    chunk: (int64 * IMessageChunk) option
}

type MessageReaderGrain(readerOptions: MessageReaderOptions) =
    inherit Grain()

    let mutable state = {
        partition = None
        quota = 0L
        nextSequence = 0L
        chunk = None
    }
    let subs = ObserverSubscriptionManager<IMessageReaderObserver>()

    member this.FromInitialized() = task {
        match state.partition with
        | Some partition -> return partition
        | None -> return raise <| invalidOp "Message reader must be initialized before any other operation is used"
    }

    member this.EnsureChunk() =
        let factory = this.GrainFactory
        task {
            let! partition = this.FromInitialized()
            let chunkIndex = state.nextSequence / readerOptions.chunkSize
        
            match state.chunk with
            | Some (currentIndex, _) ->
                if currentIndex <> chunkIndex then
                    do! this.ReleaseChunk()
            | _ -> do ()

            match state.chunk with
            | None ->
                let chunk = factory.GetGrain<IMessageChunk>(chunkIndex, partition, null)
                do! chunk.Subscribe(this)
                do state <- { state with chunk = Some(chunkIndex, chunk) }
            | _ -> do ()
        }

    member this.ReleaseChunk() = task {
        match state.chunk with
        | Some (_, chunk) ->
            do! chunk.Unsubscribe(this)
            do state <- { state with chunk = None }
        | None ->
            do ()
    }

    // Try to remove timer

    member this.RefreshReaderImpl (): Task<unit> =
        task {
            let shouldTry =
                if subs.Count = 0 then false
                elif state.quota <= 0L then false
                else true

            if not shouldTry then
                do! this.ReleaseChunk()
            else
                do! this.EnsureChunk()
                match state.chunk with
                | Some (chunkIndex, chunk) ->
                    let fromSeq = state.nextSequence
                    let chunkEnd = (chunkIndex + 1L) * readerOptions.chunkSize
                    let quotaEnd = fromSeq + int64 state.quota
                    let toSeq = min chunkEnd quotaEnd
                    let! result = chunk.FromSequenceRange(fromSeq, toSeq)
                    let nextSequence =
                        result.messages
                        |> List.tryLast
                        |> Option.bind (OptLens.getOpt Message.sequence)
                        |> Option.map ((+) 1L)
                        |> Option.defaultValue state.nextSequence
                    do state <- { state with quota = state.quota - (nextSequence - state.nextSequence) }
                    do state <- { state with nextSequence = nextSequence }
                    if not result.chunkCouldReceiveMoreMessages then
                        do! this.ReleaseChunk()
                    let endOfCurrentTopic =
                        // If chunk is still open and could not read up to toSeq
                        result.chunkCouldReceiveMoreMessages &&
                        nextSequence < chunkEnd
                    let listResult: MessageListResult = {
                        messages = result.messages
                        endOfCurrentTopic = endOfCurrentTopic // keep reading?
                    }
                    do subs.Notify(fun obs -> obs.AcceptMessages listResult)
                    if nextSequence < toSeq then
                        return()
                    else
                        return! this.RefreshReaderImpl ()

                | None -> do()
        }

    member this.InitializeImpl (init: MessageReaderInit) = task {
        let sequence = 
            match init.readFrom with
            | ReadFromStart -> 0L
            | ReadFromSequence sequence ->
                if sequence < 0L then raise <| invalidArg "sequence" "cannot be negative"
                sequence
        do state <- { state with nextSequence = sequence }

        if init.initialQuota < 0L then
            raise <| invalidArg "initialQuota" "cannot be negative"
        do state <- { state with quota = init.initialQuota }

        do state <- { state with partition = Some init.topicId }

        return! this.RefreshReaderImpl ()
    }

    member this.IssueQuotaImpl count = task {
        if count < 0 then
            return raise <| invalidArg "count" "cannot be negative"
        do state <- { state with quota = state.quota + int64 count }
        return! this.RefreshReaderImpl ()
    }

    member this.SubscribeImpl observer = task {
        if subs.IsSubscribed observer |> not then
            do subs.Subscribe observer
            return! this.RefreshReaderImpl ()
    }

    member this.UnsubscribeImpl observer = task {
        if subs.IsSubscribed observer then
            do subs.Unsubscribe observer
    }

    interface IMessageReaderRefresh with
        member this.RefreshReader available =
            this.RefreshReaderImpl ()

    interface IMessageReader with
        member this.Initialize init =
            this.InitializeImpl init

        member this.IssueQuota count =
            this.IssueQuotaImpl count

        member this.Subscribe observer =
            this.SubscribeImpl observer

        member this.Unsubscribe observer =
            this.UnsubscribeImpl observer

    interface IMessageChunkObserver with
        member this.MessagesAvailable() =
            let partition = this.GetPrimaryKey()
            let self = this.GrainFactory.GetGrain<IMessageReaderRefresh>(partition)
            // TODO: Do this work? It makes me uncomfortable the ignore!!!
            do self.RefreshReader true |> ignore

        member this.MessagesComplete() =
            // let partition = this.GetPrimaryKey()
            // let self = this.GrainFactory.GetGrain<IMessageReaderRefresh>(partition)
            // TODO: Do this work? It makes me uncomfortable the ignore!!!
            // do self.RefreshReader false |> ignore
            do ()
