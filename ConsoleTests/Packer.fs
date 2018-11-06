module MsgPack.FSharp.Packer
open MsgPack
open System.IO

let fromBuffer buffer = Packer.Create(buffer: byte[])
let fromStream stream = Packer.Create(stream: Stream)

let inline internal query fn (pk: Packer) = fn pk
let inline internal make fn = query (fun pk -> fn pk; pk)

let dispose pk = pk |> make (fun p -> p.Dispose())
let flush pk = pk |> make (fun p -> p.Flush())
let flushAsync pk = pk |> query (fun p -> p.FlushAsync() |> Async.AwaitTask)
let flushAsyncWith token pk = pk |> query (fun p -> p.FlushAsync(token) |> Async.AwaitTask)

module Pack =
    open System.Linq
    open System.Collections.Generic
    open System

    let internal toDict map =
        (Map.toSeq map).ToDictionary((fun (k, _) -> k), (fun (_, v) -> v));

    let arrayCountHeader count = query (fun p -> p.PackArrayHeader(count: int))
    let arrayHeader list = query (fun p -> p.PackArrayHeader(list: IList<_>))
    let array list = query (fun p -> p.PackArray(list: _ seq))
    let arrayWith context list = query (fun p -> p.PackArray((list: _ seq), context))

    let binaryHeader length = query (fun p -> p.PackBinaryHeader(length))
    let binary value = query (fun p -> p.PackBinary((value: byte[])))
    let binarySeq value = query (fun p -> p.PackBinary((value: byte seq)))
    let binaryList value = query (fun p -> p.PackBinary((value: byte IList)))

    let stringHeader length = query (fun p -> p.PackStringHeader(length))
    let string value = query (fun p -> p.PackString((value: string)))
    let stringSeq value = query (fun p -> p.PackString((value: char seq)))
    let stringWith encoding value = query (fun p -> p.PackString((value: string), encoding))
    let stringSeqWith encoding value = query (fun p -> p.PackString((value: char seq), encoding))

    let mapCountHeader count = query (fun p -> p.PackMapHeader((count: int)))
    let mapDictHeader map = query (fun p -> p.PackMapHeader((map: IDictionary<_, _>)))
    let mapDict map = query (fun p -> p.PackMap((map: IDictionary<_, _>)))
    let mapDictWith context map = query (fun p -> p.PackMap((map: IDictionary<_, _>), context))
    let mapHeader map = map |> Map.count |> mapCountHeader
    let map map = map |> toDict |> mapDict
    let mapWith context map = map |> toDict |> mapDictWith context

    let null' pk = pk |> query (fun p -> p.PackNull())
    let pack<'a> value = query (fun p -> p.Pack((value: 'a)))
    let packWith<'a> context value = query (fun p -> p.Pack((value: 'a), context))
    let bool value = query (fun p -> p.Pack((value: bool)))
    let float value = query (fun p -> p.Pack((value: float)))
    let float32 value = query (fun p -> p.Pack((value: float32)))
    let byte value = query (fun p -> p.Pack((value: byte)))
    let sbyte value = query (fun p -> p.Pack((value: sbyte)))
    let int16 value = query (fun p -> p.Pack((value: int16)))
    let uint16 value = query (fun p -> p.Pack((value: uint16)))
    let int value = query (fun p -> p.Pack((value: int)))
    let uint32 value = query (fun p -> p.Pack((value: uint32)))
    let int64 value = query (fun p -> p.Pack((value: int64)))
    let uint64 value = query (fun p -> p.Pack((value: uint64)))
    let nullableBool value = query (fun p -> p.Pack((value: bool Nullable)))
    let nullableFloat value = query (fun p -> p.Pack((value: float Nullable)))
    let nullableFloat32 value = query (fun p -> p.Pack((value: float32 Nullable)))
    let nullableByte value = query (fun p -> p.Pack((value: byte Nullable)))
    let nullableSByte value = query (fun p -> p.Pack((value: sbyte Nullable)))
    let nullableInt16 value = query (fun p -> p.Pack((value: int16 Nullable)))
    let nullableUInt16 value = query (fun p -> p.Pack((value: uint16 Nullable)))
    let nullableInt value = query (fun p -> p.Pack((value: int Nullable)))
    let nullableUInt32 value = query (fun p -> p.Pack((value: uint32 Nullable)))
    let nullableInt64 value = query (fun p -> p.Pack((value: int64 Nullable)))
    let nullableUInt64 value = query (fun p -> p.Pack((value: uint64 Nullable)))
    let optBool value = value |> Option.toNullable |> nullableBool
    let optFloat value = value |> Option.toNullable |> nullableFloat
    let optFloat32 value = value |> Option.toNullable |> nullableFloat32
    let optByte value = value |> Option.toNullable |> nullableByte
    let optSByte value = value |> Option.toNullable |> nullableSByte
    let optInt16 value = value |> Option.toNullable |> nullableInt16
    let optUInt16 value = value |> Option.toNullable |> nullableUInt16
    let optInt value = value |> Option.toNullable |> nullableInt
    let optUInt32 value = value |> Option.toNullable |> nullableUInt32
    let optInt64 value = value |> Option.toNullable |> nullableInt64
    let optUInt64 value = value |> Option.toNullable |> nullableUInt64
    // TODO: ASYNC && ASYNC Cancelable

    module Async =
        open System.Threading.Tasks
        open MsgPack.Serialization
        open System.Text

        let inline internal query fn (pk: Packer) = fn pk |> Async.AwaitTask
        let inline internal queryUn (fn: _ -> Task) (pk: Packer) = fn pk |> Async.AwaitTask
        let inline internal make fn (pk: Packer) = async.Combine(queryUn fn pk, async.Return pk)

        let arrayCountHeader count = make (fun p -> p.PackArrayHeaderAsync(count: int))
        let arrayHeader list = make (fun p -> p.PackArrayHeaderAsync(list: IList<_>))
        let array list = make (fun p -> p.PackArrayAsync(list: _ seq))
        let arrayWith context list = make (fun p -> p.PackArrayAsync((list: _ seq), (context: SerializationContext)))

        let binaryHeader length = make (fun p -> p.PackBinaryHeaderAsync(length))
        let binary value = make (fun p -> p.PackBinaryAsync((value: byte[])))
        let binarySeq value = make (fun p -> p.PackBinaryAsync((value: byte seq)))
        let binaryList value = make (fun p -> p.PackBinaryAsync((value: byte IList)))

        let stringHeader length = make (fun p -> p.PackStringHeaderAsync(length))
        let string value = make (fun p -> p.PackStringAsync((value: string)))
        let stringSeq value = make (fun p -> p.PackStringAsync((value: char seq)))
        let stringWith encoding value = make (fun p -> p.PackStringAsync((value: string), (encoding: Encoding)))
        let stringSeqWith encoding value = make (fun p -> p.PackStringAsync((value: char seq), (encoding: Encoding)))

        let mapCountHeader count = make (fun p -> p.PackMapHeaderAsync((count: int)))
        let mapDictHeader map = make (fun p -> p.PackMapHeaderAsync((map: IDictionary<_, _>)))
        let mapDict map = make (fun p -> p.PackMapAsync((map: IDictionary<_, _>)))
        let mapDictWith context map = make (fun p -> p.PackMapAsync((map: IDictionary<_, _>), (context: SerializationContext)))
        let mapHeader map = map |> Map.count |> mapCountHeader
        let map map = map |> toDict |> mapDict
        let mapWith context map = map |> toDict |> mapDictWith context

        let null' pk = pk |> make (fun p -> p.PackNullAsync())
        let bool value = make (fun p -> p.PackAsync((value: bool)))

        module Cancellable =
            open System.Threading

            let arrayCountHeader token count = make (fun p -> p.PackArrayHeaderAsync((count: int), token))
            let arrayHeader token list = make (fun p -> p.PackArrayHeaderAsync((list: IList<_>), token))
            let array token list = make (fun p -> p.PackArrayAsync((list: _ seq), (token: CancellationToken)))
            let arrayWith token context list = make (fun p -> p.PackArrayAsync((list: _ seq), (context: SerializationContext), (token: CancellationToken)))

            let binaryHeader token length = make (fun p -> p.PackBinaryHeaderAsync(length, token))
            let binary token value = make (fun p -> p.PackBinaryAsync((value: byte[]), token))
            let binarySeq token value = make (fun p -> p.PackBinaryAsync((value: byte seq), token))
            let binaryList token value = make (fun p -> p.PackBinaryAsync((value: byte IList), token))

            let stringHeader token length = make (fun p -> p.PackStringHeaderAsync(length, (token: CancellationToken)))
            let string token value = make (fun p -> p.PackStringAsync((value: string), (token: CancellationToken)))
            let stringSeq token value = make (fun p -> p.PackStringAsync((value: char seq), (token: CancellationToken)))
            let stringWith token encoding value = make (fun p -> p.PackStringAsync((value: string), (encoding: Encoding), (token: CancellationToken)))
            let stringSeqWith token encoding value = make (fun p -> p.PackStringAsync((value: char seq), (encoding: Encoding), (token: CancellationToken)))

            let mapCountHeader token count = make (fun p -> p.PackMapHeaderAsync((count: int), (token: CancellationToken)))
            let mapDictHeader token map = make (fun p -> p.PackMapHeaderAsync((map: IDictionary<_, _>), (token: CancellationToken)))
            let mapDict token map = make (fun p -> p.PackMapAsync((map: IDictionary<_, _>), (token: CancellationToken)))
            let mapDictWith token context map = make (fun p -> p.PackMapAsync((map: IDictionary<_, _>), (context: SerializationContext), (token: CancellationToken)))
            let mapHeader token map = map |> Map.count |> mapCountHeader token
            let map token map = map |> toDict |> mapDict token
            let mapWith token context map = map |> toDict |> mapDictWith token context

            let null' token = make (fun p -> p.PackNullAsync(token))
            let bool token value = make (fun p -> p.PackAsync((value: bool), token))
