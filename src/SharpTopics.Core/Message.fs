namespace SharpTopics.Core

open SharpFunky

type Message = {
    messageId: string
    sequence: int64
    timestamp: int64
    contentType: string
    data: byte[]
}

module Message =

    let empty = {
        messageId = ""
        sequence = -1L
        timestamp = -1L
        contentType = "application/octet-stream"
        data = [||]
    }

    [<RequireQualifiedAccess>]
    module Lenses =
        let messageId = Lens.cons' (fun m -> m.messageId) (fun v m -> { m with messageId = v })
        let sequence = Lens.cons' (fun m -> m.sequence) (fun v m -> { m with sequence = v })
        let timestamp = Lens.cons' (fun m -> m.timestamp) (fun v m -> { m with timestamp = v })
        let contentType = Lens.cons' (fun m -> m.contentType) (fun v m -> { m with contentType = v })
        let data = Lens.cons' (fun m -> m.data) (fun v m -> { m with data = v })

    let data = Lens.get Lenses.data
    let setData = Lens.set Lenses.data

    let messageId = Lens.get Lenses.messageId
    let setMessageId = Lens.set Lenses.messageId

    let sequence = Lens.get Lenses.sequence
    let setSequence = Lens.set Lenses.sequence

    let timestamp = Lens.get Lenses.timestamp
    let setTimestamp = Lens.set Lenses.timestamp

    let contentType = Lens.get Lenses.contentType
    let setContentType = Lens.set Lenses.contentType
