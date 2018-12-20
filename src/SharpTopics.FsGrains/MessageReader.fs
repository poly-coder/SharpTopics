﻿namespace SharpTopics.FsGrains

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
    inherit IGrainWithStringKey

    abstract RefreshReader: available: bool -> Task<unit>

type internal MessageReaderState = {
    partition: string
    quota: int
    nextSequence: int64
    timer: IDisposable option
    chunk: (int64 * IMessageChunk) option
    available: bool
}

type MessageReaderGrain(readerOptions: MessageReaderOptions) =
    inherit Grain()

    let state = ref {
        partition = ""
        quota = 0
        nextSequence = 0L
        timer = None
        chunk = None
        available = true
    }
    let subs = ObserverSubscriptionManager<IMessageReaderObserver>()

    member this.BaseRegisterTimer asyncCallback dueTime period =
        this.RegisterTimer(
            Func<_, _> (fun _ -> asyncCallback()), null, 
            TimeSpan.FromSeconds dueTime, 
            TimeSpan.FromSeconds period)

    override this.OnActivateAsync() =
        task {
            do state := { !state with partition = this.GetPrimaryKeyString() }
        } :> Task

    member this.EnsureTimer() =
        match state.Value.timer with
        | None ->
            let timer =
                this.RegisterTimer(
                    (fun _ -> task { do! this.RefreshReaderImpl false } :> Task), null, 
                    TimeSpan.FromSeconds <| readerOptions.timerPeriod,
                    TimeSpan.FromSeconds <| readerOptions.timerPeriod)
            do state := { !state with timer = Some timer }
        | _ ->
            do ()

    member this.ReleaseTimer() =
        match state.Value.timer with
        | Some timer ->
            do timer.Dispose()
            do state := { !state with timer = None }
        | None ->
            do ()

    member this.EnsureChunk() =
        let factory = this.GrainFactory
        task {
            let chunkIndex = state.Value.nextSequence / readerOptions.chunkSize
        
            match state.Value.chunk with
            | Some (currentIndex, _) ->
                if currentIndex <> chunkIndex then
                    do! this.ReleaseChunk()
            | _ -> do ()

            match state.Value.chunk with
            | None ->
                let chunk = factory.GetGrain<IMessageChunk>(chunkIndex, state.Value.partition, null)
                do! chunk.Subscribe(this)
                do state := { !state with chunk = Some(chunkIndex, chunk) }
            | _ -> do ()
        }

    member this.ReleaseChunk() = task {
        match state.Value.chunk with
        | Some (_, chunk) ->
            do! chunk.Unsubscribe(this)
            do state := { !state with chunk = None }
        | None ->
            do ()
    }

    member this.RefreshReaderImpl available: Task<unit> =
        task {
            do state := { !state with available = state.Value.available || available }
            let shouldTry =
                if subs.Count = 0 then false
                elif state.Value.quota <= 0 then false
                elif not state.Value.available && not available then false
                else true

            if not shouldTry then
                do this.ReleaseTimer()
                do! this.ReleaseChunk()
            else
                do! this.EnsureChunk()
                match state.Value.chunk with
                | Some (chunkIndex, chunk) ->
                    let fromSeq = state.Value.nextSequence
                    let chunkEnd = (chunkIndex + 1L) * readerOptions.chunkSize
                    let quotaEnd = fromSeq + int64 state.Value.quota
                    let toSeq = min chunkEnd quotaEnd
                    let! result = chunk.FromSequenceRange(fromSeq, toSeq)
                    do state := { !state with nextSequence = toSeq }
                    let! moreAvailable, allMessagesHasBeenRead = task {
                        if result.chunkCouldReceiveMoreMessages then
                            if result.requestHasMoreMessagesInChunk then
                                return true, false
                            else
                                return true, true
                        else
                            // The chunk is done
                            do! this.ReleaseChunk()
                            return false, false
                    }
                    let listResult: MessageListResult = {
                        messages = result.messages
                        allMessagesHasBeenRead = allMessagesHasBeenRead // keep reading?
                    }
                    do this.NotifySubscribers listResult
                    return! this.RefreshReaderImpl moreAvailable

                | None -> do()
        }

    member this.StartFromSequenceImpl sequence = task {
        if sequence < 0L then
            return raise <| invalidArg "sequence" "cannot be negative"
        do state := { !state with nextSequence = sequence }
        return! this.RefreshReaderImpl false
    }

    member this.IssueQuotaImpl count = task {
        if count < 0 then
            return raise <| invalidArg "count" "cannot be negative"
        do state := { !state with quota = state.Value.quota + count }
        return! this.RefreshReaderImpl false
    }

    member this.SubscribeImpl observer = task {
        if subs.IsSubscribed observer |> not then
            do subs.Subscribe observer
            return! this.RefreshReaderImpl false
    }

    member this.UnsubscribeImpl observer = task {
        if subs.IsSubscribed observer then
            do subs.Unsubscribe observer
    }

    member this.NotifySubscribers result =
        subs.Notify(fun obs -> obs.AcceptMessages result)

    interface IMessageReaderRefresh with
        member this.RefreshReader available =
            this.RefreshReaderImpl available

    interface IMessageReader with
        member this.StartFromSequence sequence =
            this.StartFromSequenceImpl sequence

        member this.IssueQuota count =
            this.IssueQuotaImpl count

        member this.Subscribe observer =
            this.SubscribeImpl observer

        member this.Unsubscribe observer =
            this.UnsubscribeImpl observer

    interface IMessageChunkObserver with
        member this.MessagesAvailable() =
            let partition = this.GetPrimaryKeyString()
            let self = this.GrainFactory.GetGrain<IMessageReaderRefresh>(partition)
            // TODO: Do this work? It makes me uncomfortable the ignore!!!
            do self.RefreshReader true |> ignore

        member this.MessagesComplete() =
            let partition = this.GetPrimaryKeyString()
            let self = this.GrainFactory.GetGrain<IMessageReaderRefresh>(partition)
            // TODO: Do this work? It makes me uncomfortable the ignore!!!
            do self.RefreshReader false |> ignore