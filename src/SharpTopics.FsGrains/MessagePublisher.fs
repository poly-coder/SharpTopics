namespace SharpTopics.FsGrains

open Orleans
open System
open SharpTopics.Core
open SharpTopics.FsInterfaces
open FSharp.Control.Tasks.V2
open System.Threading.Tasks
open SharpFunky

type internal MessagePublisherState = {
    partition: string
    status: MessageStoreStatus
}

type MessagePublisherGrain(store: IMessageStore) =
    inherit Grain()

    let mutable state = {
        partition = ""
        status = MessageStoreStatus.empty
    }
    let subs = ObserverSubscriptionManager<IMessagePublisherObserver>()
    let genId() = Guid.NewGuid().ToString("N")

    override this.OnActivateAsync() =
        task {
            let partition = this.GetPrimaryKeyString()
            let! status = store.fetchStatus partition
            do state <-
                {
                    state with
                        partition = partition
                        status = status
                }
        } :> Task

    interface IMessagePublisher with
        member this.PublishMessages messages = task {
            if state.status.isFrozen then
                return invalidOp "Topic is frozen. It cannot public any more messages for now"
            elif messages |> Seq.isEmpty then
                return invalidOp "You must publish at least one message" // ???
            else
                let timestampNow = DateTime.UtcNow.Ticks
                let mutable nextSequence = state.status.nextSequence
                let messages' =
                    messages
                    |> Seq.map (fun message ->
                        let message' = 
                            message
                            |> OptLens.setSome Message.timestamp timestampNow
                            |> OptLens.setSome Message.sequence nextSequence
                            |> OptLens.upd Message.messageId (function Some id -> Some id | None -> Some <| genId())
                        nextSequence <- nextSequence + 1L
                        message'
                    )
                    |> Seq.toList
                let metas' = messages' |> List.map (Lens.get Message.meta)
                do state <- { state with status = { state.status with nextSequence = nextSequence } }
                do! store.storeMessagesAndStatus state.partition messages' state.status
                do subs.Notify(fun obs -> obs.MessagesPublished())
                return metas'
        }

        member this.Subscribe subscriber = task {
            if subs.IsSubscribed subscriber |> not then
                subs.Subscribe subscriber
        }

        member this.Unsubscribe subscriber = task {
            if subs.IsSubscribed subscriber then
                subs.Unsubscribe subscriber
        }
