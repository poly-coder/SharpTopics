module MongoTesting

open MongoDB.Bson
open MongoDB.Bson.Serialization
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver
open MongoDB.Driver.Core
open MongoDB.Driver.Linq
open SharpTopics.MongoImpl
open SharpTopics.Core

//type Contact = {
    
//}

let listDatabases (client: MongoClient) = async {
    use! cursor = client.ListDatabaseNamesAsync() |> Async.AwaitTask
    let! names = cursor.ToListAsync() |> Async.AwaitTask
    printfn "Databases: %A" (names |> Seq.toList)
}

let getOrCreateCollection<'a> name (db: IMongoDatabase) = async {
    return db.GetCollection<'a>(name)
}

let openDatabase connectionString username password dbName = 
    let settings = MongoClientSettings.FromConnectionString(connectionString);
    settings.Credential <- MongoCredential.CreateCredential(dbName, username, (password: string))
    let client = MongoClient(settings)
    client.GetDatabase(dbName)


let testMongo() = 
    async {
        let db = openDatabase "mongodb://localhost:27017" "Admin" "Admin" "admin"
        let! coll = db |> getOrCreateCollection<BsonDocument> "system.version"
        let! count = coll.EstimatedDocumentCountAsync() |> Async.AwaitTask
        printfn "Total count: %d" count
        let filter = FilterDefinition.Empty
        let! cursor = coll.FindAsync(filter) |> Async.AwaitTask
        let! list = cursor.ToListAsync() |> Async.AwaitTask
        printfn "Result: %A" (list |> Seq.toList)
    } |> Async.RunSynchronously

type KVItem = {
    [<BsonId>]
    key: string
    [<BsonRequired>]
    name: string
    [<BsonRequired>]
    age: int
}

let openKVStore collectionName =
    let db = openDatabase "mongodb://localhost:27017" "Admin" "Admin" "admin"
    let opts: KeyValueStore.Options<_> = 
        { 
            database = db
            collection = collectionName
            validateKey = fun _ -> AsyncResult.ok()
            validateValue = fun _ -> AsyncResult.ok()
            updateKey = fun key v -> { v with key = key }
        }
    opts |> KeyValueStore.createStore

let testKeyStorePut () = 
    asyncResult {
        let kvstore = openKVStore "test-kv"
        do! kvstore.put "1234" { key = ""; name = "Iskander"; age = 40 }
    }
    |> AsyncResult.getOrExn
    |> Async.RunSynchronously

let testKeyStoreGet () = 
    asyncResult {
        let kvstore = openKVStore "test-kv"
        let! item = kvstore.get "1234"
        printfn "Found item: %A" item
    }
    |> AsyncResult.getOrExn
    |> Async.RunSynchronously
