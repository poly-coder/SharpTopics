namespace SharpTopics.Core

open SharpFunky

type MetaData =
    | MetaNull
    | MetaString of string
    | MetaLong of int64
    | MetaFloat of float
    | MetaBinary of byte[]

type MessageMeta = Map<string, MetaData>

type Message = {
    meta: MessageMeta
    data: byte[] option
}

module MetaData =
    let metaString =
        OptLens.cons'
            (function MetaString s -> Some s | _ -> None)
            (fun v _ -> match v with Some s -> MetaString s | _ -> MetaNull)
    let metaLong =
        OptLens.cons'
            (function MetaLong s -> Some s | _ -> None)
            (fun v _ -> match v with Some s -> MetaLong s | _ -> MetaNull)
    let metaFloat =
        OptLens.cons'
            (function MetaFloat s -> Some s | _ -> None)
            (fun v _ -> match v with Some s -> MetaFloat s | _ -> MetaNull)
    let metaBinary =
        OptLens.cons'
            (function MetaBinary s -> Some s | _ -> None)
            (fun v _ -> match v with Some s -> MetaBinary s | _ -> MetaNull)

module MessageMeta =
    let MessageIdKey = "MsgID"
    let SequenceKey = "Sequence"
    let TimestampKey = "Timestamp"
    let ContentTypeKey = "ContentType"

    let empty: MessageMeta = Map.empty
    let get key =
        Map.tryFind key >> Option.defaultValue MetaNull
    let add key = function
        | MetaNull -> Map.remove key
        | value -> Map.add key value
    let remove key = Map.remove key
    let mapKey key =
        Lens.cons' (get key) (add key)

    let metaDataKey (key: string): Lens<MessageMeta, MetaData> = mapKey key
    let internal ofMetaKey lens key = OptLens.compose (OptLens.ofLens <| metaDataKey key) lens
    let stringKey = ofMetaKey MetaData.metaString
    let longKey = ofMetaKey MetaData.metaLong
    let floatKey = ofMetaKey MetaData.metaFloat
    let binaryKey = ofMetaKey MetaData.metaBinary

    let messageId = stringKey MessageIdKey
    let contentType = stringKey ContentTypeKey
    let sequence = longKey SequenceKey
    let timestamp = longKey TimestampKey

module Message =
    let empty = {
        meta = MessageMeta.empty
        data = None
    }

    let data = OptLens.cons' (fun m -> m.data) (fun v m -> { m with data = v })
    let meta = Lens.cons' (fun m -> m.meta) (fun v m -> { m with meta = v })

    let metaDataKey (key: string) = OptLens.compose (OptLens.ofLens meta) (OptLens.ofLens <| MessageMeta.metaDataKey key)
    let internal ofMetaKey lens key = OptLens.compose (metaDataKey key) lens
    let stringKey = ofMetaKey MetaData.metaString
    let longKey = ofMetaKey MetaData.metaLong
    let floatKey = ofMetaKey MetaData.metaFloat
    let binaryKey = ofMetaKey MetaData.metaBinary

    let messageId = stringKey MessageMeta.MessageIdKey
    let contentType = stringKey MessageMeta.ContentTypeKey
    let sequence = longKey MessageMeta.SequenceKey
    let timestamp = longKey MessageMeta.TimestampKey
