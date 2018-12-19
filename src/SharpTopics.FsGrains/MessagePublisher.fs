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
