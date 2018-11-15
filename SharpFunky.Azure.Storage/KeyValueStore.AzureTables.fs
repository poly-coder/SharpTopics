module SharpFunky.Storage.KeyValueStore.AzureTables

open System
open SharpFunky
open SharpFunky.Conversion
open SharpFunky.Storage
open Microsoft.WindowsAzure.Storage.Table
open Microsoft.WindowsAzure.Storage
open System.IO

type Options<'a> = {
    table: CloudTable
    partitionKey: string
    rowKeyPrefix: string
    converter: IAsyncReversibleConverter<'a, (Map<string, string> * string)>
    updateKey: string -> 'a -> 'a
    dataColumnName: string
}

[<RequireQualifiedAccess>]
module Options =
    let from partitionKey table converter = 
        {
            table = table
            converter = converter
            partitionKey = partitionKey
            rowKeyPrefix = ""
            dataColumnName = "__Data"
            updateKey = fun _ v -> v
        }
    let withRowKeyPrefix value = fun opts -> { opts with rowKeyPrefix = value }
    let withUpdateKey value = fun opts -> { opts with updateKey = value }
    let withDataColumnName value = fun opts -> { opts with dataColumnName = value }

let private prKeysSet = ["PartitionKey"; "RowKey"] |> Set.ofList
let fromOptions opts =
    let partitionCond =
        TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, opts.partitionKey)

    let getRowKey key =
        sprintf "%s%s" opts.rowKeyPrefix key

    let makeQuery key =
        let rowKey = getRowKey key
        let filter =
            TableQuery.CombineFilters(
                partitionCond,
                TableOperators.And,
                TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, rowKey)
            )
        TableQuery().Where(filter)

    let extractData (item: DynamicTableEntity) = asyncResult {
        let data = ref None
        let map = ref Map.empty
        let err = ref None
        for p in item.Properties do
            match !err with
            | Some _ -> ()
            | None ->
                match p.Key with
                | key when Set.contains key prKeysSet -> ()
                | key when key = opts.dataColumnName ->
                    match !data with
                    | None when p.Value.PropertyType = EdmType.String ->
                        data := Some p.Value.StringValue
                    | None ->
                        err := sprintf "Data column type should be string but %A found" p.Value.PropertyType |> exn |> Some
                    | Some _ ->
                        err := sprintf "Duplicate data column" |> exn |> Some
                | key ->
                    match p.Value.PropertyType with
                    | EdmType.String ->
                        map := !map |> Map.add key p.Value.StringValue
                    | propType ->
                        err := sprintf "%s column type should be string but %A found" key propType |> exn |> Some
        match !err with
        | None ->
            match !data with
            | None -> 
                return! "Data column not found" |> exn |> AsyncResult.error
            | Some data ->
                return data, !map
        | Some e -> return! AsyncResult.error e
    }
        
    let get key =
        asyncResult {
            let query = makeQuery key
            let! segment = opts.table.ExecuteQuerySegmentedAsync(query, null) |> AsyncResult.ofTask
            match segment.Results |> Seq.tryHead with
            | None -> return None
            | Some item ->
                let! data, meta = extractData item
                let! result = opts.converter.convertBack(meta, data)
                return Some result
        }

    let put key value =
        asyncResult {
            let! converted = opts.converter.convert value
            let meta, bytes = converted
            let blobName = getBlobName key
            let blobRef = opts.table.GetBlockBlobReference(blobName)
            do! blobRef.FetchAttributesAsync() |> AsyncResult.ofTaskVoid
            for k, v in meta |> Map.toSeq do
                do blobRef.Metadata.[k] <- v
            do! blobRef.SetMetadataAsync() |> AsyncResult.ofTaskVoid
            do! blobRef.UploadFromByteArrayAsync(bytes, 0, bytes.Length) |> AsyncResult.ofTaskVoid
        }
        
    let del key =
        asyncResult {
            let blobName = getBlobName key
            let blobRef = opts.table.GetBlockBlobReference(blobName)
            do! blobRef.DeleteIfExistsAsync() |> AsyncResult.ofTaskVoid
        }

    KeyValueStore.createInstance get put del
