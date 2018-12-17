namespace SharpTopics.FsGrains

open Orleans
open System
open SharpTopics.Core
open SharpTopics.FsInterfaces
open FSharp.Control.Tasks.V2
open System.Threading.Tasks
open SharpFunky

type MessagePublisherGrain(store: IMessageStore) =
    inherit Grain()

    let status = ref MessageStoreStatus.empty

    override this.OnActivateAsync() =
        task {
            let! st = store.fetchStatus()
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
                            |> OptLens.setSome Message.Lenses.timestamp timestampNow
                            |> OptLens.setSome Message.Lenses.sequence st.Value.nextSequence
                        st := { !st with nextSequence = st.Value.nextSequence + 1L }
                        message'
                    )
                    |> Seq.toList
                let metas' = messages' |> Seq.map (Lens.get Message.Lenses.meta)
                do! store.storeMessagesAndStatus messages' st.Value
                status := !st
                return metas'
        }
