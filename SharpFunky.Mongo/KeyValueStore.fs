module SharpFunky.Storage.KeyValueStore.Mongo

open System
open MongoDB.Driver
open MongoDB.Driver.Core
open SharpFunky
open SharpFunky.Storage

type Options<'t> = {
    database: IMongoDatabase
    collection: string
    updateKey: string -> 't -> 't
}

[<RequireQualifiedAccess>]
module Options =
    let from database collection = 
        {
            database = database
            collection = collection
            updateKey = fun _ v -> v
        }
    let withUpdateKey value = fun opts -> { opts with updateKey = value }

let fromOptions opts =
    let settings = MongoCollectionSettings()
    settings.AssignIdOnInsert <- false
    let collection = opts.database.GetCollection<'t>(opts.collection, settings)
    let filters = Builders<'t>.Filter
    let _idField = StringFieldDefinition<'t, string> "_id"
    let findOpts = FindOptions<'t, 't>() |> tee (fun x -> x.Limit <- Nullable 1)

    let get key = async {
        try
            let filter = filters.Eq(_idField, key)
            let! cursor = collection.FindAsync(filter, findOpts) |> Async.AwaitTask
            let! list = cursor.ToListAsync() |> Async.AwaitTask
            return Seq.tryHead list |> Ok
        with exn ->
            return Error exn
    }

    let put key value = async {
        try
            let value' = opts.updateKey key value
            do! collection.InsertOneAsync(value') |> Async.AwaitTask
            return Ok value'
        with exn ->
            return Error exn
    }

    let del key = async {
        try
            let filter = filters.Eq(_idField, key)
            let! result = collection.DeleteOneAsync(filter) |> Async.AwaitTask
            ignore result
            return Ok ()
        with exn ->
            return Error exn
    }
    KeyValueStore.createInstance get put del
