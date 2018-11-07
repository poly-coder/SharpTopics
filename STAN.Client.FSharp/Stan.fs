module STAN.Client.FSharp.Stan

open STAN.Client
open System

module Options =
    let def () = StanOptions.GetDefaultOptions()

    let inline private make fn (opts: StanOptions) = fn opts; opts

    let withConnectionTimeout timeout = make <| fun o -> o.ConnectTimeout <- timeout
    let withDiscoverPrefix prefix = make <| fun o -> o.DiscoverPrefix <- prefix
    let withMaxPubAcksInFlight value = make <| fun o -> o.MaxPubAcksInFlight <- value
    let withNatsConn conn = make <| fun o -> o.NatsConn <- conn
    let withNatsURL url = make <| fun o -> o.NatsURL <- url
    let withPubAckWait value = make <| fun o -> o.PubAckWait <- value

module SubOptions =
    let def () = StanSubscriptionOptions.GetDefaultOptions()

    let inline private make fn (opts: StanSubscriptionOptions) = fn opts; opts

    let withAckWait value = make <| fun o -> o.AckWait <- value
    let withDurableName value = make <| fun o -> o.DurableName <- value
    let withManualAcks value = make <| fun o -> o.ManualAcks <- value
    let withMaxInflight value = make <| fun o -> o.MaxInflight <- value
        
    let deliverAllAvailable opts = opts |> make (fun o -> o.DeliverAllAvailable())
    let startWithLastReceived opts = opts |> make (fun o -> o.StartWithLastReceived())
    let startAtTime time = make (fun o -> o.StartAt(time: DateTime))
    let startAtDuration duration = make (fun o -> o.StartAt(duration: TimeSpan))
    let startAtSequence sequence = make (fun o -> o.StartAt(sequence: uint64))
 
module Connect =
    let private defaultConnection = StanConnectionFactory()
    let inline private make fn = fn defaultConnection

    let it clusterID clientID =
        make <| fun f -> f.CreateConnection(clusterID, clientID)
    let fromOptions opts clusterID clientID =
        make <| fun f -> f.CreateConnection(clusterID, clientID, opts)

let inline private query fn (conn: IStanConnection) = fn conn
let inline private make fn = query (fun c -> fn c; c)

let close conn = conn |> make (fun c -> c.Close())

let publish subject data =
    make (fun c -> c.Publish(subject, data))
let publishWith handler subject data =
    query (fun c -> c.Publish(subject, data, EventHandler<_>(fun _ args -> handler args)))
let publishAsync subject data =
    query (fun c -> c.PublishAsync(subject, data) |> Async.AwaitTask)

let subscribe subject handler =
    query (fun c -> c.Subscribe(subject, EventHandler<_>(fun _ args -> handler args)))

let subscribeOpts opts subject handler =
    query (fun c -> c.Subscribe(subject, (opts: StanSubscriptionOptions), EventHandler<_>(fun _ args -> handler args)))
