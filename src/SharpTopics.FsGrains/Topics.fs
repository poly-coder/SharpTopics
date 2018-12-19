
namespace SharpTopics.FsGrains

open Orleans
open System
open SharpTopics.Core
open SharpTopics.FsInterfaces
open FSharp.Control.Tasks.V2
open System.Threading.Tasks
open SharpFunky
open System.Collections.Generic

type MessagePublisherGrain(store: IMessageStore) =
    inherit Grain()

    let partition = ref ""
    let status = ref MessageStoreStatus.empty
    let subs = ref None
    let genId() = Guid.NewGuid().ToString("N")

    override this.OnActivateAsync() =
        task {
            subs := Some <| ObserverSubscriptionManager<IMessagePublisherObserver>()
            partition := this.GetPrimaryKeyString()
            let! st = store.fetchStatus !partition
            status := st
        } :> Task

    interface IMessagePublisher with
        member this.PublishMessages messages = task {
            if status.Value.isFrozen then
                return invalidOp "Topic is frozen. It cannot public any more messages for now"
            elif messages |> Seq.isEmpty then
                return invalidOp "You must publish at least one message" // ???
            else
                let timestampNow = DateTime.UtcNow.Ticks
                let st = ref !status
                let messages' =
                    messages
                    |> Seq.map (fun message ->
                        let message' = 
                            message
                            |> OptLens.setSome Message.timestamp timestampNow
                            |> OptLens.setSome Message.sequence st.Value.nextSequence
                            |> OptLens.upd Message.messageId (function Some id -> Some id | None -> Some <| genId())
                        st := { !st with nextSequence = st.Value.nextSequence + 1L }
                        message'
                    )
                    |> Seq.toList
                let metas' = messages' |> List.map (Lens.get Message.meta)
                do! store.storeMessagesAndStatus !partition messages' st.Value
                status := !st
                return metas'
        }

        member this.Subscribe subscriber = task {
            let subs = Option.get !subs
            if subs.IsSubscribed subscriber |> not then
                subs.Subscribe subscriber
        }

        member this.Unsubscribe subscriber = task {
            let subs = Option.get !subs
            if subs.IsSubscribed subscriber then
                subs.Unsubscribe subscriber
        }

type IMessageStoreChunkRefresh =
    inherit IGrainWithStringKey

    abstract RefreshChunk: unit -> Task<unit>

type MessageStoreChunkGrain(store: IMessageStore) =
    inherit Grain()

    let chunkSize = 1000L
    let partition = ref ""
    let index = ref -1L
    let info = ref <| ChunkInfo.empty()
    let publisher = ref None
    let subs = ref None
    let loadedMessages = List()

    override this.OnActivateAsync() =
        task {
            partition := this.GetPrimaryKeyString()
            subs := Some <| ObserverSubscriptionManager<IMessageStoreChunkObserver>()
            index := this.GetPrimaryKeyLong()
            let minSequence = !index * chunkSize
            let maxSequence = (!index + 1L) * chunkSize
            info := {
                minSequence = minSequence
                maxSequence = maxSequence
                nextSequence = minSequence
                isComplete = false
            }
            do! this.RefreshChunkImpl()
        } :> Task

    member this.RefreshChunkImpl() =
        let factory = this.GrainFactory
        task {
            if info.Value.isComplete then return ()
            let! result = store.fetchMessagesAndStatus 
                            !partition
                            info.Value.nextSequence
                            info.Value.maxSequence
            info := 
                { !info with
                    nextSequence = result.nextSequence
                    isComplete = result.nextSequence = info.Value.maxSequence
                }
            loadedMessages.AddRange result.messages
            if not info.Value.isComplete then
                match !publisher with
                | None ->
                    let pub = factory.GetGrain<IMessagePublisher>(!partition)
                    publisher := Some pub
                    do! pub.Subscribe this
                    return! this.RefreshChunkImpl()
                | Some _ ->
                    return ()
            else
                match !publisher with
                | Some pub ->
                    do! pub.Unsubscribe this
                    publisher := None
                | None -> ()
                return ()
        }

    member this.GetChunkInfoImpl() = task {
        return !info
    }

    member this.FromSequenceRangeImpl fromSeq toSeq = task {
        let inRange msg =
            match OptLens.getOpt Message.sequence msg with
            | Some s -> fromSeq <= s && s < toSeq
            | None -> false
        let messages = loadedMessages |> Seq.filter inRange |> List.ofSeq
        let result = {
            messages = messages
        }
        return result
    }

    member this.SubscribeImpl observer = task {
        let subs = Option.get !subs
        if subs.IsSubscribed observer |> not then
            subs.Subscribe observer
    }

    member this.UnsubscribeImpl observer = task {
        let subs = Option.get !subs
        if subs.IsSubscribed observer then
            subs.Unsubscribe observer
    }

    interface IMessageStoreChunkRefresh with
        member this.RefreshChunk() =
            this.RefreshChunkImpl()

    interface IMessageStoreChunk with
        member this.GetChunkInfo() =
            this.GetChunkInfoImpl()

        member this.FromSequenceRange(fromSeq, toSeq) =
            this.FromSequenceRangeImpl fromSeq toSeq

        member this.Subscribe observer =
            this.SubscribeImpl observer

        member this.Unsubscribe observer =
            this.UnsubscribeImpl observer

    interface IMessagePublisherObserver with
        member this.MessagesPublished() =
            let self = this.GrainFactory.GetGrain<IMessageStoreChunkRefresh>(this.GetPrimaryKeyString())
            // TODO: Do this work? It makes me uncomfortable the ignore!!!
            self.RefreshChunk()
            |> ignore