module NatsStoring
open NATS.Client
open NATS.Client.FSharp
open STAN.Client
open STAN.Client.FSharp
open SharpTopics.Core
open System.Text
open System.Threading.Tasks
open System
open SerializationTests

let startPublishing count (conn: IConnection) =
    async {
        for i in 1 .. count do
            let str = sprintf "Hello #%d" i
            printfn "Sending: %s" str
            let bytes = Encoding.UTF8.GetBytes str
            conn |> Nats.publishData "test" bytes |> ignore
            do! Async.Sleep 500
    } |> Async.StartAsTask

let startConsumming (conn: IConnection) =
    let lastAccess = ref DateTime.Now
    let sub =
        conn |> Nats.subscribeAsyncWith (fun args ->
            let msg = args.Message
            let str = Encoding.UTF8.GetString(msg.Data)
            lastAccess := DateTime.Now
            printfn "Received: %s" str) "test"
    sub.Start()
    async {
        let br = ref false
        while not !br do
            let diff = (DateTime.Now - !lastAccess).TotalMilliseconds
            // printfn "Waiting: %f" diff
            if diff <= 1000.0 
            then do! Async.Sleep 100
            else br := true
    } |> Async.StartAsTask

let testNatsCommunication() =
    let opts =
        Nats.Options.def()
        |> Nats.Options.withUrl "nats://localhost:4222"
    printfn "NATS options: \n%A" opts
    printfn "NATS url: \n%s" opts.Url
    use conn = Nats.Connect.fromOpts(opts)
    let task1 = conn |> startPublishing 10
    let task2 = conn |> startConsumming
    Task.WaitAll(task1, task2)

let testStanPublish() =
    let opts =
        Stan.Options.def()
        |> Stan.Options.withNatsURL "nats://localhost:4223"
    use conn = Stan.Connect.fromOptions opts "test-cluster" "12345"
    let ser = TopicMessageSerializer.json()
    for i in 1 .. 10 do
        let msg = makeMessageUp (Some i)
        let bytes = ser.serialize msg |> Async.RunSynchronously
        conn |> Stan.publish "test" bytes |> ignore
        printfn "Sent bytes: %d" bytes.Length

let testStanSubscribe() =
    let opts =
        Stan.Options.def()
        |> Stan.Options.withNatsURL "nats://localhost:4223"
    printfn "STAN url: \n%s" opts.NatsURL
    use conn = Stan.Connect.fromOptions opts "test-cluster" "12346"
    let ser = TopicMessageSerializer.json()
    let left = ref 10
    let sopts =
        Stan.SubOptions.def()
        |> Stan.SubOptions.withDurableName "test-sub"
        |> Stan.SubOptions.withManualAcks true
        |> Stan.SubOptions.withAckWait 1000
        |> Stan.SubOptions.deliverAllAvailable
    let sub =
        conn 
        |> Stan.subscribeOpts sopts "test" (fun args ->
            let msg = args.Message
            printfn "Received bytes: %d" msg.Data.Length
            left := !left - 1
            msg.Ack()
            do ser.deserialize msg.Data |> Async.RunSynchronously |> ignore)
    async {
        while !left > 0 do
            do! Async.Sleep 100
    } |> Async.RunSynchronously

