namespace SharpTopics.Core

open SharpFunky

type MessageMeta = Map<string, string>

type Message = {
    meta: MessageMeta
    data: byte[] option
}

module MessageMeta =
    let MessageIdKey = "MsgID"
    let SequenceKey = "Sequence"
    let TimestampKey = "Timestamp"
    let ContentTypeKey = "Content-Type"

    let empty: MessageMeta = Map.empty

    [<RequireQualifiedAccess>]
    module Lenses =
        let stringKey (key: string): OptLens<_, string> = OptLens.mapKey key
        let longKey key = OptLens.compose (stringKey key) OptLens.longParser
        
        let messageId = stringKey MessageIdKey
        let contentType = stringKey ContentTypeKey
        let sequence = longKey SequenceKey
        let timestamp = longKey TimestampKey

module Message =
    let empty = {
        meta = MessageMeta.empty
        data = None
    }

    [<RequireQualifiedAccess>]
    module Lenses =
        let data = OptLens.cons' (fun m -> m.data) (fun v m -> { m with data = v })
        let meta = Lens.cons' (fun m -> m.meta) (fun v m -> { m with meta = v })

        let metaKey key = OptLens.compose (OptLens.ofLens meta) (OptLens.mapKey key)
        let longKey key = OptLens.compose (metaKey key) OptLens.longParser

        let ofMeta lens = OptLens.compose (OptLens.ofLens meta) lens
        let messageId = ofMeta MessageMeta.Lenses.messageId
        let contentType = ofMeta MessageMeta.Lenses.contentType
        let sequence = ofMeta MessageMeta.Lenses.sequence
        let timestamp = ofMeta MessageMeta.Lenses.timestamp

