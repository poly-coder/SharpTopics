namespace SharpTopics.AzureStorage

open SharpFunky
open System.Threading.Tasks
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open FSharp.Control.Tasks.V2
open SharpTopics.Core

type AzureTableMessageStoreOptions = {
    statusRowKey: string
    messageRowKeyPrefix: string
    metaPrefix: string
    dataPrefix: string
    maxDataChunkSize: int
}

module AzureTableMessageStoreOptions =
    let empty = {
        statusRowKey = "A_STATUS"
        messageRowKeyPrefix = "MSG_"
        metaPrefix = "META_"
        dataPrefix = "DATA_"
        maxDataChunkSize = 65536
    }

type MessageStoreStatusEntity(pk, rk) =
    inherit TableEntity(pk, rk)
    member val IsFrozen = false with get, set
    member val NextSequence = 0L with get, set
    member this.toStatus() = {
        isFrozen = this.IsFrozen
        nextSequence = this.NextSequence
    }
    static member ofStatus pk rk status =
        let entity = MessageStoreStatusEntity(pk, rk)
        entity.ETag <- "*"
        entity.IsFrozen <- status.isFrozen
        entity.NextSequence <- status.nextSequence
        entity

type AzureTableMessageStore(table: CloudTable, options: AzureTableMessageStoreOptions) =
    
    let messageToEntity partition message =
        let entity = DynamicTableEntity()
        do entity.PartitionKey <- partition

        match OptLens.getOpt Message.Lenses.sequence message with
            | Some sequence -> entity.RowKey <- sprintf "%s%i" options.messageRowKeyPrefix sequence
            | None -> ()

        message.meta
            |> Map.toSeq
            |> Seq.filter (fun (key, _) ->
                key <> MessageMeta.SequenceKey)
            |> Seq.iter (fun (key, value) ->
                entity.Properties.Add(
                    sprintf "%s%s" options.metaPrefix key,
                    EntityProperty(value)))

        entity

    interface IMessageStore with
        member this.fetchStatus partition = task {
            let operation = TableOperation.Retrieve<MessageStoreStatusEntity>(partition, options.statusRowKey)
            let! statusResult = table |> execute operation
            if isNull statusResult.Result then
                return MessageStoreStatus.empty
            else
                return (statusResult.Result :?> MessageStoreStatusEntity).toStatus()
        }

        member this.storeMessagesAndStatus partition messages status = task {
            let batch = TableBatchOperation()
            let statusEntity = MessageStoreStatusEntity.ofStatus partition options.statusRowKey status
            do batch.InsertOrMerge(statusEntity)
            do messages
                |> List.iter (fun message ->
                    let entity = messageToEntity partition message
                    do batch.Insert(entity))
            let! _ = table |> executeBatch batch
            return ()
        }
