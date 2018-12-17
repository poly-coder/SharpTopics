namespace SharpTopics.AzureStorage

open System.Threading.Tasks
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open FSharp.Control.Tasks.V2
open SharpTopics.Core

type AzureTableMessageStoreOptions = {
    partitionKey: string
    statusRowKey: string
    messageRowKeyPrefix: string
}

module AzureTableMessageStoreOptions =
    let empty = {
        partitionKey = "topic"
        statusRowKey = "A_STATUS"
        messageRowKeyPrefix = "M_"
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

module MessageEntity = // DynamicTableEntity

type AzureTableMessageStore(table: CloudTable, options: AzureTableMessageStoreOptions) =
    interface IMessageStore with
        member this.fetchStatus() = task {
            let operation = TableOperation.Retrieve<MessageStoreStatusEntity>(options.partitionKey, options.statusRowKey)
            let! statusResult = table |> execute operation
            if isNull statusResult.Result then
                return MessageStoreStatus.empty
            else
                return (statusResult.Result :?> MessageStoreStatusEntity).toStatus()
        }

        member this.storeMessagesAndStatus messages status = task {
            let batch = TableBatchOperation()
            let statusEntity = MessageStoreStatusEntity.ofStatus options.partitionKey options.statusRowKey status
            do batch.InsertOrMerge(statusEntity)
            do messages
                |> List.iter (fun message ->
                    
                )
        }
