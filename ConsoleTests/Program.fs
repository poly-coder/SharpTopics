open SerializationTests
open NatsStoring
open MongoTesting

[<EntryPoint>]
let main argv =
    // testPickler()
    // testNatsCommunication()
    // testStanPublish()
    // testStanSubscribe()
    testKeyStorePut()
    // testKeyStoreGet()
    
    0
