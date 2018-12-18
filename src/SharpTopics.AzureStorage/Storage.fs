namespace Microsoft.WindowsAzure.Storage

open SharpFunky
open System.Collections.Generic
open FSharp.Control.Tasks.V2

[<AutoOpen>]
module Storage =
    let developmentAccount =
        CloudStorageAccount.DevelopmentStorageAccount

    let parseAccount connectionString =
        CloudStorageAccount.Parse(connectionString)

    let tryParseAccount connectionString =
        CloudStorageAccount.TryParse(connectionString)
        |> Option.ofTryOp


module Table =
    open Microsoft.WindowsAzure.Storage.Table

    let name (table: CloudTable) = table.Name
    let client (table: CloudTable) = table.ServiceClient
    let storageUri (table: CloudTable) = table.StorageUri
    let uri (table: CloudTable) = table.Uri
    
    let execute operation (table: CloudTable) =
        table.ExecuteAsync(operation)
    let executeWith requestOptions operationContext operation (table: CloudTable) =
        table.ExecuteAsync(operation, requestOptions, operationContext)

    let executeBatch operation (table: CloudTable) =
        table.ExecuteBatchAsync(operation)
    let executeBatchWith requestOptions operationContext operation (table: CloudTable) =
        table.ExecuteBatchAsync(operation, requestOptions, operationContext)

    let executeQuerySegmented (query: TableQuery) token (table: CloudTable) =
        table.ExecuteQuerySegmentedAsync(query, token)
    let executeQueryOfSegmented (query: TableQuery<_>) token (table: CloudTable) =
        table.ExecuteQuerySegmentedAsync(query, token)

    let executeQueryFull query table =
        let list = List()
        let rec loop token = task {
            let! segment = table |> executeQuerySegmented query token
            list.AddRange segment
            if isNull segment.ContinuationToken then
                return list
            else
                return! loop segment.ContinuationToken
        }
        loop null

    let executeQueryOfFull query table =
        let list = List()
        let rec loop token = task {
            let! segment = table |> executeQueryOfSegmented query token
            list.AddRange segment
            if isNull segment.ContinuationToken then
                return list
            else
                return! loop segment.ContinuationToken
        }
        loop null

    let columnEqualsTo value column = TableQuery.GenerateFilterCondition(column, QueryComparisons.Equal, value)
    let partitionEqualsTo partition = "PartitionKey" |> columnEqualsTo partition
    let rowKeyEqualsTo partition = "RowKey" |> columnEqualsTo partition