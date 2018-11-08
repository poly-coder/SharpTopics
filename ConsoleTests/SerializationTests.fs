module SerializationTests

open SharpTopics.Core
open System
open MsgPack.FSharp
open System.IO
open System.Text

let toBase64 bytes = Convert.ToBase64String bytes
let fromUtf8 bytes = Encoding.UTF8.GetString(bytes: byte[])

let showSerializer name toStr (serializer: ITopicMessageSerializer) msg =
    printfn "\n%s" name
    async {
        let! binData = serializer.serialize msg
        let str = toStr binData
        printfn "%d bytes: %s" binData.Length str
    } |> Async.RunSynchronously

let packMeta meta =
    let rec packStringList ls =
        match ls with
        | [] -> Packer.null'
        | s :: ls -> Packer.string s >> packStringList ls
        
    let packMetaValue value =
        match value with
        | MetaString s -> Packer.byte 1uy >> Packer.string s
        | MetaUInt64 v -> Packer.byte 2uy >> Packer.uint64 v
        | MetaInt64 v -> Packer.byte 3uy >> Packer.int64 v
        | MetaStringList v -> Packer.byte 4uy >> packStringList v
        | MetaStringSet v -> Packer.byte 4uy >> Packer.array v
    let packValues pk =
        (pk, Map.toSeq meta)
        ||> Seq.fold (fun pk (key, value) ->
            pk
            |> Packer.string key
            |> packMetaValue value)
    Packer.int (Map.count meta) >> packValues


let msgpack() =
    let ser msg = async {
        use stream = new MemoryStream()
        let pk = Packer.fromStream(stream)
        pk 
        |> Packer.uint16 1us
        |> packMeta msg.meta
        |> Packer.binary msg.data
        |> Packer.flush
        |> ignore
        return stream.ToArray()
    }

    let deser bytes = async.Return TopicMessage.empty

    TopicMessageSerializer.makeSerializer ser deser

let toBinaryPickle msg =
    showSerializer "Binary pickle" toBase64 (TopicMessageSerializer.binary()) msg

let toXmlPickle msg =
    showSerializer "XML pickle" fromUtf8 (TopicMessageSerializer.xml()) msg

let toJsonPickle msg =
    showSerializer "JSON pickle" fromUtf8 (TopicMessageSerializer.json()) msg

let toMsgPackPickle msg =
    showSerializer "MsgPack pickle" toBase64 (msgpack()) msg

let randomString (rnd: Random) (abc: string) minLen maxLen =
    let len = rnd.Next(minLen, maxLen + 1)
    let chars = [| for _ in 1 .. len -> abc.Chars(rnd.Next(0, abc.Length)) |]
    String(chars)

let randomStringList (rnd: Random) abc minLen maxLen minStringLen maxStringLen =
    let len = rnd.Next(minLen, maxLen + 1)
    [ for _ in 1 .. len -> randomString rnd abc minStringLen maxStringLen ]

let randomArray (rnd: Random) minLen maxLen converter minValue maxValue =
    let len = rnd.Next(minLen, maxLen + 1)
    [| for _ in 1 .. len -> rnd.Next(minValue, maxValue + 1) |> converter |]

let makeMessageUp seed =
    let rnd = match seed with None -> Random() | Some s -> Random(s)
    let hexes = "0123456789ABCDEF"
    TopicMessage.empty
    |> TopicMessage.setMessageId (randomString rnd hexes 16 32)
    |> TopicMessage.setSequence (rnd.Next() |> uint64)
    |> TopicMessage.setTimestamp (rnd.Next() |> uint64)
    |> TopicMessage.MetaStringList.set "transform" (randomStringList rnd hexes 1 10 16 32)
    |> TopicMessage.MetaStringSet.addMany "tags" (randomStringList rnd hexes 0 10 16 32)
    |> TopicMessage.setData (randomArray rnd 10 128 byte 0 255)

let testPickler () =
    let msg = makeMessageUp (Some 2)
    printfn "Message: %A" msg
    toBinaryPickle msg
    toXmlPickle msg
    toJsonPickle msg
    toMsgPackPickle msg

