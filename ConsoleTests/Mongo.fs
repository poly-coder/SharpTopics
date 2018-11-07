module MongoTesting

open MongoDB.Bson
open MongoDB.Bson.Serialization
open MongoDB.Bson.Serialization.Attributes
open MongoDB.Driver
open MongoDB.Driver.Core
open MongoDB.Driver.Linq

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

let testMongo() = 
    async {
        let settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.Credential <- MongoCredential.CreateCredential("admin", "Admin", "Admin")
        let client = MongoClient(settings)
        // do! listDatabases client
        let db = client.GetDatabase("admin")
        let! coll = db |> getOrCreateCollection<BsonDocument> "system.version"
        let! count = coll.EstimatedDocumentCountAsync() |> Async.AwaitTask
        printfn "Total count: %d" count
        let filter = FilterDefinition.Empty
        let! cursor = coll.FindAsync(filter) |> Async.AwaitTask
        let! list = cursor.ToListAsync() |> Async.AwaitTask
        printfn "Result: %A" (list |> Seq.toList)
    } |> Async.RunSynchronously


