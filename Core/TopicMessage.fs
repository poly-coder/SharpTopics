namespace SharpTopics.Core

type MetaData =
    | MetaString of string
    | MetaUInt64 of uint64
    | MetaInt64 of int64
    | MetaStringList of string list
    | MetaStringSet of string Set

type TopicMetaData = Map<string, MetaData>

type TopicMessage = {
    meta: TopicMetaData
    data: byte[]
}

module TopicMessage =
    let empty: TopicMessage = {
        meta = Map.empty
        data = [| |]
    }

    let data msg = msg.data
    let setData value msg = { msg with data = value }

    let meta msg = msg.meta
    let setMeta value msg = { msg with meta = value }
    let updMeta updFn msg = setMeta (meta msg |> updFn) msg
    
    let metaKey key msg = meta msg |> Map.tryFind key
    let setMetaKey key value = updMeta (Map.add key value)
    let unsetMetaKey key = updMeta (Map.remove key)
    let updMetaKey key updFn = updMeta (fun map -> 
        Map.tryFind key map 
        |> updFn 
        |> function Some v -> Map.add key v map | _ -> Map.remove key map)

    module MetaString =
        let defaultValue = ""
        let toTyped = Option.bind <| function MetaString s -> Some s | _ -> None
        let fromTyped = function 
            | Some s when not (s = defaultValue) -> Some (MetaString s) 
            | _ -> None

        let getOpt key = metaKey key >> toTyped
        let getOr defVal key = getOpt key >> Option.defaultValue defVal
        let get key = getOr defaultValue key
        let updOpt key updFn = updMetaKey key (toTyped >> updFn >> fromTyped)
        let set key value = updOpt key (fun _ -> Some value)
        let unset key = updOpt key (fun _ -> None)
        let upd key updFn = updOpt key ((function Some s -> updFn s | None -> updFn defaultValue) >> Some)

    module MetaUInt64 =
        let defaultValue = uint64 0
        let toTyped = Option.bind <| function MetaUInt64 s -> Some s | _ -> None
        let fromTyped = function 
            | Some s when not (s = defaultValue) -> Some (MetaUInt64 s) 
            | _ -> None

        let getOpt key = metaKey key >> toTyped
        let getOr defVal key = getOpt key >> Option.defaultValue defVal
        let get key = getOr defaultValue key
        let updOpt key updFn = updMetaKey key (toTyped >> updFn >> fromTyped)
        let set key value = updOpt key (fun _ -> Some value)
        let unset key = updOpt key (fun _ -> None)
        let upd key updFn = updOpt key ((function Some s -> updFn s | None -> updFn defaultValue) >> Some)

    module MetaInt64 =
        let defaultValue = int64 0
        let toTyped = Option.bind <| function MetaInt64 s -> Some s | _ -> None
        let fromTyped = function 
            | Some s when not (s = defaultValue) -> Some (MetaInt64 s) 
            | _ -> None

        let getOpt key = metaKey key >> toTyped
        let getOr defVal key = getOpt key >> Option.defaultValue defVal
        let get key = getOr defaultValue key
        let updOpt key updFn = updMetaKey key (toTyped >> updFn >> fromTyped)
        let set key value = updOpt key (fun _ -> Some value)
        let unset key = updOpt key (fun _ -> None)
        let upd key updFn = updOpt key ((function Some s -> updFn s | None -> updFn defaultValue) >> Some)

    module MetaStringList =
        let defaultValue: string list = [ ]
        let toTyped = Option.bind <| function MetaStringList s -> Some s | _ -> None
        let fromTyped = function 
            | Some s when not (s = defaultValue) -> Some (MetaStringList s) 
            | _ -> None

        let getOpt key = metaKey key >> toTyped
        let getOr defVal key = getOpt key >> Option.defaultValue defVal
        let get key = getOr defaultValue key
        let updOpt key updFn = updMetaKey key (toTyped >> updFn >> fromTyped)
        let set key value = updOpt key (fun _ -> Some value)
        let unset key = updOpt key (fun _ -> None)
        let upd key updFn = updOpt key ((function Some s -> updFn s | None -> updFn defaultValue) >> Some)
        let append key values = upd key (List.append values)
        let prepend key value = upd key (fun xs -> value :: xs)
        let contains key value = get key >> List.contains value
        let count key = get key >> List.length
        let isEmpty key = get key >> List.isEmpty
        let exists key predicate = get key >> List.exists predicate

    module MetaStringSet =
        let defaultValue: string Set = Set.empty
        let toTyped = Option.bind <| function MetaStringSet s -> Some s | _ -> None
        let fromTyped = function 
            | Some s when not (s = defaultValue) -> Some (MetaStringSet s) 
            | _ -> None

        let getOpt key = metaKey key >> toTyped
        let getOr defVal key = getOpt key >> Option.defaultValue defVal
        let get key = getOr defaultValue key
        let updOpt key updFn = updMetaKey key (toTyped >> updFn >> fromTyped)
        let set key value = updOpt key (fun _ -> Some value)
        let unset key = updOpt key (fun _ -> None)
        let upd key updFn = updOpt key ((function Some s -> updFn s | None -> updFn defaultValue) >> Some)
        let add key value = upd key (Set.add value)
        let addMany key values = upd key (values |> Set.ofSeq |> Set.union)
        let remove key value = upd key (Set.remove value)
        let contains key value = get key >> Set.contains value
        let count key = get key >> Set.count
        let isEmpty key = get key >> Set.isEmpty
        let exists key predicate = get key >> Set.exists predicate

    let MessageId = "MsgID"
    let messageIdOpt msg = MetaString.getOpt MessageId msg
    let messageId msg = MetaString.get MessageId msg
    let updMessageIdOpt fn msg = MetaString.updOpt MessageId fn msg
    let setMessageId s msg = MetaString.set MessageId s msg
    let randomMessageId msg = MetaString.set MessageId (System.Guid.NewGuid().ToString()) msg

    let SequenceId = "SeqID"
    let sequenceOpt msg = MetaUInt64.getOpt SequenceId msg
    let sequence msg = MetaUInt64.get SequenceId msg
    let setSequence s msg = MetaUInt64.set SequenceId s msg

    let TimestampId = "Timestamp"
    let timestampOpt msg = MetaUInt64.getOpt TimestampId msg
    let timestamp msg = MetaUInt64.get TimestampId msg
    let setTimestamp s msg = MetaUInt64.set TimestampId s msg

type ITopicMessageSerializer =
    abstract serialize: TopicMessage -> Async<byte[]>
    abstract deserialize: byte[] -> Async<TopicMessage>

module TopicMessageSerializer =
    open MBrace.FsPickler
    open MBrace.FsPickler.Json
    open System.IO

    let makeSerializer serialize deserialize =
        { new ITopicMessageSerializer with
            member __.serialize msg = serialize msg
            member __.deserialize data = deserialize data
        }

    let binary() =
        let serializer = FsPickler.CreateBinarySerializer()
        let serialize msg =
            use stream = new MemoryStream()
            serializer.Serialize(stream, msg)
            stream.ToArray() |> async.Return
        let deserialize data =
            use stream = new MemoryStream(data: byte[])
            let msg = serializer.Deserialize<TopicMessage>(stream)
            msg |> async.Return
        makeSerializer serialize deserialize

    let xml() =
        let serializer = FsPickler.CreateXmlSerializer()
        let serialize msg =
            use stream = new MemoryStream()
            serializer.Serialize(stream, msg)
            stream.ToArray() |> async.Return
        let deserialize data =
            use stream = new MemoryStream(data: byte[])
            let msg = serializer.Deserialize<TopicMessage>(stream)
            msg |> async.Return
        makeSerializer serialize deserialize

    let json() =
        let serializer = FsPickler.CreateJsonSerializer()
        let serialize msg =
            use stream = new MemoryStream()
            serializer.Serialize(stream, msg)
            stream.ToArray() |> async.Return
        let deserialize data =
            use stream = new MemoryStream(data: byte[])
            let msg = serializer.Deserialize<TopicMessage>(stream)
            msg |> async.Return
        makeSerializer serialize deserialize
