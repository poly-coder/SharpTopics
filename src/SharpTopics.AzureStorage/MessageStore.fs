namespace SharpTopics.AzureStorage

open SharpFunky
open System.Threading.Tasks
open Microsoft.WindowsAzure.Storage
open Microsoft.WindowsAzure.Storage.Table
open FSharp.Control.Tasks.V2
open SharpTopics.Core
open System

type AzureMessageStoreConfig() =
    member val ConnectionString = "" with get, set
    member val TableName = "" with get, set
    member val StatusRowKey = "A_STATUS" with get, set
    member val MessageRowKeyPrefix = "MSG_" with get, set
    member val MetaPrefix = "META_" with get, set
    member val DataPrefix = "DATA_" with get, set
    member val DataChunkSize = 65536 with get, set

type AzureTableMessageStore(options: AzureMessageStoreConfig) =

    let table =
        let account = Storage.parseAccount options.ConnectionString
        let client = account.CreateCloudTableClient()
        client.GetTableReference(options.TableName)

    let seqToRowKey = sprintf "%s%020i" options.MessageRowKeyPrefix
    let rowKeyToSeq =
        fun (s: string) -> s.Substring(options.MessageRowKeyPrefix.Length)
        // >> fun s -> s.TrimStart('0')
        >> Int64.parse
        

    let metaDataToEntityProperty value =
        match value with
        | MetaString v -> EntityProperty v
        | MetaLong v -> EntityProperty(Nullable v)
        | MetaFloat v -> EntityProperty(Nullable v)
        | MetaBinary v -> EntityProperty v
        | MetaNull -> invalidOp "Cannot map MetaNull to EntityProperty"

    let entityPropertyToMetaData (prop: EntityProperty) =
        match prop.PropertyType with
        | EdmType.String ->
            if isNull prop.StringValue then MetaNull else MetaString prop.StringValue
        | EdmType.Int64 ->
            if prop.Int64Value.HasValue then MetaLong prop.Int64Value.Value else MetaNull 
        | EdmType.Int32 ->
            if prop.Int32Value.HasValue then MetaLong <| int64 prop.Int32Value.Value else MetaNull 
        | EdmType.Double ->
            if prop.DoubleValue.HasValue then MetaFloat prop.DoubleValue.Value else MetaNull 
        | EdmType.Binary ->
            if isNull prop.BinaryValue then MetaNull else MetaBinary prop.BinaryValue
        | _ ->
            MetaNull

    let messageToEntity partition message =
        let entity = DynamicTableEntity()
        do entity.PartitionKey <- partition

        match OptLens.getOpt Message.sequence message with
            | Some sequence ->
                entity.RowKey <- seqToRowKey sequence
            | None -> ()

        message.meta
            |> Map.toSeq
            |> Seq.filter (fun (key, _) ->
                key <> MessageMeta.SequenceKey)
            |> Seq.iter (fun (key, value) ->
                entity.Properties.Add(
                    sprintf "%s%s" options.MetaPrefix key,
                    metaDataToEntityProperty value))

        match message.data with
        | Some data when data.Length > 0 ->
            let maxIndex = int(Math.Ceiling(float data.Length / float options.DataChunkSize)) - 1
            seq { 0 .. maxIndex }
            |> Seq.iter (fun index ->
                let initIndex = index * options.DataChunkSize
                let chunkSize = min options.DataChunkSize (data.Length - initIndex)
                // TODO: Optimize with ArrayPool
                let bytes = Array.zeroCreate chunkSize // (fun i -> data.[i + initIndex])
                Array.Copy(data, initIndex, bytes, 0, chunkSize)
                entity.Properties.Add(
                    sprintf "%s%i" options.DataPrefix index,
                    EntityProperty(bytes))
            )
        | _ ->
            do ()

        entity

    let entityToMessage (entity: DynamicTableEntity) =
        let sequence =
            if entity.RowKey.StartsWith options.MessageRowKeyPrefix then
                rowKeyToSeq entity.RowKey
            else
                invalidOp <| sprintf "Found a message with an invalid key: %s - %s" entity.PartitionKey entity.RowKey

        let data =
            let dataProps =
                entity.Properties
                |> Seq.map (fun p -> p.Key, p.Value)
                |> Seq.filter (fun (k, _) -> k.StartsWith(options.DataPrefix))
                |> Seq.map (fun (k, v) -> (k.Substring(options.DataPrefix.Length) |> Int32.parse), v)
                |> Seq.sortBy fst
                |> Seq.map snd
                |> Seq.map (fun p ->
                    if p.PropertyType = EdmType.Binary then p.BinaryValue
                    else invalidOp <| sprintf "Found a message with an invalid data column: %s - %s" entity.PartitionKey entity.RowKey
                )
                |> Seq.toList
            match dataProps with
            | [] -> None
            | _ ->
                let dataLength =
                    dataProps
                    |> List.sumBy (fun b -> b.Length)
                let bytes = Array.zeroCreate dataLength
                let mutable index = 0
                dataProps
                    |> List.iter (fun b ->
                        Array.Copy(b, 0, bytes, index, b.Length)
                        index <- index + b.Length)
                Some bytes
        
        let meta =
            entity.Properties
            |> Seq.map (fun p -> p.Key, p.Value)
            |> Seq.filter (fun (k, _) -> k.StartsWith(options.MetaPrefix))
            |> Seq.map (fun (k, v) -> k.Substring(options.DataPrefix.Length), entityPropertyToMetaData v)
            |> Seq.toList
            

        Message.empty
            |> OptLens.set Message.data data
            |> OptLens.setSome Message.sequence sequence
            |> fun message ->
                (message, meta)
                ||> Seq.fold (fun m (k, v) ->
                    m |> OptLens.setSome (Message.metaDataKey k) v)

    let statusToEntity partition status =
        let entity = DynamicTableEntity(partition, options.StatusRowKey)
        do entity.ETag <- "*"
        do entity.Properties.Add("IsFrozen", EntityProperty(Nullable status.isFrozen))
        do entity.Properties.Add("NextSequence", EntityProperty(Nullable status.nextSequence))
        entity

    let entityToStatus (entity: DynamicTableEntity) =
        let isFrozen =
            entity.Properties.Item("IsFrozen")
            |> fun prop -> if isNull prop then Nullable() else prop.BooleanValue
            |> fun nullable -> nullable.GetValueOrDefault MessageStoreStatus.empty.isFrozen
        let nextSequence =
            entity.Properties.Item("NextSequence")
            |> fun prop -> if isNull prop then Nullable() else prop.Int64Value
            |> fun nullable -> nullable.GetValueOrDefault MessageStoreStatus.empty.nextSequence
        {
            isFrozen = isFrozen
            nextSequence = nextSequence
        }

    interface IMessageStore with
        member this.fetchStatus partition = task {
            let operation = TableOperation.Retrieve(partition, options.StatusRowKey)
            let! statusResult = table |> execute operation
            if isNull statusResult.Result then
                return MessageStoreStatus.empty
            else
                return statusResult.Result :?> DynamicTableEntity |> entityToStatus
        }

        member this.storeMessagesAndStatus partition messages status = task {
            let batch = TableBatchOperation()
            let statusEntity = statusToEntity partition status
            do batch.InsertOrMerge(statusEntity)
            do messages
                |> List.iter (fun message ->
                    let entity = messageToEntity partition message
                    do batch.Insert(entity))
            let! _ = table |> executeBatch batch
            return ()
        }

        member this.fetchMessagesAndStatus partition from toExclusive = task { //Task<FetchMessagesResult>
            let where =
                TableQuery.CombineFilters(
                    partitionEqualsTo partition,
                    TableOperators.And,
                    rowKeyEqualsTo options.StatusRowKey
                )
            let query = TableQuery().Where(where).Take(Nullable 1)
            let! queryResult = table |> executeQueryFull query
            if queryResult.Count = 1 then
                let nextSequence =
                    queryResult.[0]
                    |> entityToStatus
                    |> fun s -> s.nextSequence
                let where =
                    TableQuery.CombineFilters(
                        partitionEqualsTo partition,
                        TableOperators.And,
                        TableQuery.CombineFilters(
                            rowKeyGreaterThanOrEqualTo (seqToRowKey from),
                            TableOperators.And,
                            rowKeyLessThan (seqToRowKey toExclusive)
                        )
                    )
                let query = TableQuery().Where(where)
                let! queryResult =
                    table
                    |> executeQueryFull query
                let messages =
                    queryResult
                    |> Seq.map entityToMessage
                    |> Seq.toList
                return {
                    messages = messages
                    nextSequence = nextSequence
                }
            else
                return {
                    messages = []
                    nextSequence = 0L
                }
        }
