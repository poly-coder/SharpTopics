module SharpTopics.MongoImpl.KeyValueStore
open System
open MongoDB.Bson
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver
open MongoDB.Driver.Core
open SharpTopics.Core
open SharpTopics.Core.KeyValueStore

type Options<'t> = {
    database: IMongoDatabase
    collection: string
    validateKey: string -> Async<Result<unit, exn>>
    validateValue: 't -> Async<Result<unit, exn>>
    updateKey: string -> 't -> 't
}

let createStore (opts: Options<'t>) =
    let settings = MongoCollectionSettings()
    settings.AssignIdOnInsert <- false
    let collection = opts.database.GetCollection<'t>(opts.collection, settings)
    let filters = Builders<'t>.Filter
    let _idField = StringFieldDefinition<'t, ObjectId> "_id"
    let oid key = ObjectId(key: string)
    let findOpts =
        let x = FindOptions<'t, 't>()
        x.Limit <- Nullable 1
        x

    let get key = async {
        try
            let filter = filters.Eq(_idField, oid key)
            let! cursor = collection.FindAsync(filter, findOpts) |> Async.AwaitTask
            let! list = cursor.ToListAsync() |> Async.AwaitTask
            return Seq.tryHead list |> Ok
        with exn -> return Error exn
    }

    let put key value = async {
        try
            let value' = opts.updateKey key value
            do! collection.InsertOneAsync(value') |> Async.AwaitTask
            return Ok ()
        with exn -> return Error exn
    }

    let del key = async {
        try
            let filter = filters.Eq(_idField, oid key)
            let! result = collection.DeleteOneAsync(filter) |> Async.AwaitTask
            ignore result
            return Ok ()
        with exn -> return Error exn
    }

    let kvOptions = {
        get = get
        put = put
        del = del
        validateKey = opts.validateKey
        validateValue = opts.validateValue
    }

    makeKeyValueStore kvOptions
