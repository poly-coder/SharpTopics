namespace SharpTopics.Core

type MetaData =
    | MetaString of string
    | MetaUInt64 of uint64
    | MetaInt64 of int64
    | MetaStringList of string list

type TopicMessage = {
    meta: Map<string, MetaData>
    tags: string Set
    data: byte[]
}

module TopicMessage =
    let empty: TopicMessage = {
        meta = Map.empty
        tags = Set.empty
        data = [| |]
    }

    let data msg = msg.data
    let setData value msg = { msg with data = value }

    let tags msg = msg.tags
    let setTags value msg = { msg with tags = value }
    let updTags updFn msg = setTags (tags msg |> updFn) msg
    let addTag tag = updTags (Set.add tag)
    let removeTag tag = updTags (Set.remove tag)
    let unionTags tags = updTags (Set.union tags)
    let intersectTags tags = updTags (Set.intersect tags)

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
