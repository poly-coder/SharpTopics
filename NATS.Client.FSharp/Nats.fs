module NATS.Client.FSharp.Nats

open NATS.Client
open System.Threading
open System

module Options =
    let def() = ConnectionFactory.GetDefaultOptions()

    let internal upd fn (opts: Options) = 
        do fn opts
        opts

    let withAllowReconnect value = upd <| fun o -> o.AllowReconnect <- value
    let withMaxPingsOut value = upd <| fun o -> o.MaxPingsOut <- value
    let withMaxReconnect value = upd <| fun o -> o.MaxReconnect <- value
    let withName value = upd <| fun o -> o.Name <- value
    let withNoRandomize value = upd <| fun o -> o.NoRandomize <- value
    let withPassword value = upd <| fun o -> o.Password <- value
    let withPedantic value = upd <| fun o -> o.Pedantic <- value
    let withPingInterval value = upd <| fun o -> o.PingInterval <- value
    let withReconnectWait value = upd <| fun o -> o.ReconnectWait <- value
    let withSecure value = upd <| fun o -> o.Secure <- value
    let withServers value = upd <| fun o -> o.Servers <- value
    let withSubChannelLength value = upd <| fun o -> o.SubChannelLength <- value
    let withSubscriberDeliveryTaskCount value = upd <| fun o -> o.SubscriberDeliveryTaskCount <- value
    let withSubscriptionBatchSize value = upd <| fun o -> o.SubscriptionBatchSize <- value
    let withTimeout value = upd <| fun o -> o.Timeout <- value
    let withToken value = upd <| fun o -> o.Token <- value
    let withUrl value = upd <| fun o -> o.Url <- value
    let withUseOldRequestStyle value = upd <| fun o -> o.UseOldRequestStyle <- value
    let withUser value = upd <| fun o -> o.User <- value
    let withVerbose value = upd <| fun o -> o.Verbose <- value

    let onAsyncErrorHandler handler = upd <| fun o -> o.AsyncErrorEventHandler <- handler
    let onAsyncError fn = onAsyncErrorHandler (EventHandler<_>(fun _ args -> fn args))
    let onClosedHandler handler = upd <| fun o -> o.ClosedEventHandler <- handler
    let onClosed fn = onClosedHandler (EventHandler<_>(fun _ args -> fn args))
    let onDisconnectedHandler handler = upd <| fun o -> o.DisconnectedEventHandler <- handler
    let onDisconnected fn = onDisconnectedHandler (EventHandler<_>(fun _ args -> fn args))
    let onReconnectedHandler handler = upd <| fun o -> o.ReconnectedEventHandler <- handler
    let onReconnected fn = onReconnectedHandler (EventHandler<_>(fun _ args -> fn args))
    let onServerDiscoveredHandler handler = upd <| fun o -> o.ServerDiscoveredEventHandler <- handler
    let onServerDiscovered fn = onServerDiscoveredHandler (EventHandler<_>(fun _ args -> fn args))
    let onTLSHandler handler = upd <| fun o -> o.TLSRemoteCertificationValidationCallback <- handler
    let onTLS fn = onTLSHandler (System.Net.Security.RemoteCertificateValidationCallback(fun _ certificate chain sslPolicyErrors -> fn certificate chain sslPolicyErrors))

module Connect =
    let private defaultConnection = ConnectionFactory()
    let inline private make fn = fn defaultConnection

    let it() = make <| fun f -> f.CreateConnection()
    let fromUrl url = make <| fun f -> f.CreateConnection(url: string)
    let fromOpts opts = make <| fun f -> f.CreateConnection(opts: Options)

    module Secure =
        let fromUrl url = make <| fun f -> f.CreateSecureConnection(url: string)

    module Encoded =
        let it() = make <| fun f -> f.CreateEncodedConnection()
        let fromUrl url = make <| fun f -> f.CreateEncodedConnection(url: string)
        let fromOpts opts = make <| fun f -> f.CreateEncodedConnection(opts: Options)

let inline private query fn (conn: IConnection) = fn conn
let inline private make fn = query (fun c -> fn c; c)

let close conn = conn |> make (fun c -> c.Close())
    
let flush conn = conn |> make (fun c -> c.Flush())
let flushWith timeout = make (fun c -> c.Flush(timeout))
    
let isClosed conn = conn |> query (fun c -> c.IsClosed())
let isReconnecting conn = conn |> query (fun c -> c.IsReconnecting())
    
let newInbox conn = conn |> query (fun c -> c.NewInbox())
    
let request subject data =
    query (fun c -> c.Request(subject, data))
let requestTimeout timeout subject data =
    query (fun c -> c.Request(subject, data, timeout))
let requestAsync subject data =
    query (fun c -> c.RequestAsync(subject, data) |> Async.AwaitTask)
let requestAsyncWith token subject data =
    query (fun c -> c.RequestAsync(subject, data, (token: CancellationToken)) |> Async.AwaitTask)
let requestTimeoutAsync timeout subject data =
    query (fun c -> c.RequestAsync(subject, data, (timeout: int)) |> Async.AwaitTask)
let requestTimeoutAsyncWith token timeout subject data =
    query (fun c -> c.RequestAsync(subject, data, (timeout: int), (token: CancellationToken)) |> Async.AwaitTask)

let subscribe subject =
    query (fun c -> c.SubscribeSync(subject))
let subscribeTo queue subject =
    query (fun c -> c.SubscribeSync(subject, queue))
let subscribeAsync subject =
    query (fun c -> c.SubscribeAsync(subject))
let subscribeAsyncTo queue subject =
    query (fun c -> c.SubscribeAsync(subject, (queue: string)))
let subscribeAsyncWith handler subject =
    query (fun c -> c.SubscribeAsync(subject, EventHandler<_>(fun _ args -> handler args)))
let subscribeAsyncToWith handler queue subject =
    query (fun c -> c.SubscribeAsync(subject, queue, EventHandler<_>(fun _ args -> handler args)))
    
let resetStats conn = conn |> make (fun c -> c.ResetStats())

let publish m = make <| fun c -> c.Publish(m: Msg)
let publishData subject data =
    make <| fun c -> c.Publish(subject, data)
let publishWithReply reply subject data =
    make <| fun c -> c.Publish(subject, reply, data)
let publishDataRange offset count subject data =
    make <| fun c -> c.Publish(subject, data, offset, count)
let publishWithReplyRange reply offset count subject data =
    make <| fun c -> c.Publish(subject, reply, data, offset, count)
