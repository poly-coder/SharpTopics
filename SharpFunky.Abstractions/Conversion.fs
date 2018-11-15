namespace SharpFunky.Conversion

open SharpFunky

type ISyncConverter<'a, 'b> =
    abstract convert: 'a -> Result<'b, exn>

type IAsyncConverter<'a, 'b> =
    abstract convert: 'a -> AsyncResult<'b, exn>

type ISyncReversibleConverter<'a, 'b> =
    inherit ISyncConverter<'a, 'b>
    abstract convertBack: 'b -> Result<'a, exn>

type IAsyncReversibleConverter<'a, 'b> =
    inherit IAsyncConverter<'a, 'b>
    abstract convertBack: 'b -> AsyncResult<'a, exn>

module SyncConverter =
    let createInstance convert =
        { new ISyncConverter<'a, 'b> with
            member __.convert a = convert a }

    let toFun (converter: ISyncConverter<'a, 'b>) : ResultFn<_, _, _> =
        fun a -> converter.convert a

    let compose cab cbc =
        toFun cab |> ResultFn.bind (toFun cbc) |> createInstance

module AsyncConverter =
    let createInstance convert =
        { new IAsyncConverter<'a, 'b> with
            member __.convert a = convert a }

    let toFun (converter: IAsyncConverter<'a, 'b>) : AsyncResultFn<_, _, _> =
        fun a -> converter.convert a

    let compose cab cbc =
        toFun cab |> AsyncResultFn.bind (toFun cbc) |> createInstance

module AsyncReversibleConverter =
    let createInstance convert convertBack =
        { new IAsyncReversibleConverter<'a, 'b> with
            member __.convert a = convert a
            member __.convertBack b = convertBack b }

    let toFun (converter: IAsyncReversibleConverter<'a, 'b>) : AsyncResultFn<_, _, _> =
        fun a -> converter.convert a

    let toFunBack (converter: IAsyncReversibleConverter<'a, 'b>) : AsyncResultFn<_, _, _> =
        fun a -> converter.convertBack a

    let rev converter =
        let convert = toFun converter
        let convertBack = toFunBack converter
        createInstance convertBack convert

    let compose cab cbc =
        let convert = toFun cab |> AsyncResultFn.bind (toFun cbc)
        let convertBack = toFunBack cbc |> AsyncResultFn.bind (toFunBack cab)
        createInstance convert convertBack

module SyncReversibleConverter =
    let createInstance convert convertBack =
        { new ISyncReversibleConverter<'a, 'b> with
            member __.convert a = convert a
            member __.convertBack b = convertBack b }

    let toFun (converter: ISyncReversibleConverter<'a, 'b>) : ResultFn<_, _, _> =
        fun a -> converter.convert a

    let toFunBack (converter: ISyncReversibleConverter<'a, 'b>) : ResultFn<_, _, _> =
        fun a -> converter.convertBack a

    let rev converter =
        let convert = toFun converter
        let convertBack = toFunBack converter
        createInstance convertBack convert

    let compose cab cbc =
        let convert = toFun cab |> ResultFn.bind (toFun cbc)
        let convertBack = toFunBack cbc |> ResultFn.bind (toFunBack cab)
        createInstance convert convertBack

module Converters =
    let toBase64 =
        SyncReversibleConverter.createInstance (String.toBase64 >> Ok) (String.fromBase64Res)
    let fromBase64 = SyncReversibleConverter.rev toBase64

    let fromUtf8 =
        SyncReversibleConverter.createInstance (Result.catch String.fromUtf8) (String.toUtf8 >> Ok)
    let toUtf8 = SyncReversibleConverter.rev fromUtf8

    let fromUtf7 =
        SyncReversibleConverter.createInstance (Result.catch String.fromUtf7) (String.toUtf7 >> Ok)
    let toUtf7 = SyncReversibleConverter.rev fromUtf7

    let fromUtf32 =
        SyncReversibleConverter.createInstance (Result.catch String.fromUtf32) (String.toUtf32 >> Ok)
    let toUtf32 = SyncReversibleConverter.rev fromUtf32

    let fromUnicode =
        SyncReversibleConverter.createInstance (Result.catch String.fromUnicode) (String.toUnicode >> Ok)
    let toUnicode = SyncReversibleConverter.rev fromUnicode

    let fromAscii =
        SyncReversibleConverter.createInstance (Result.catch String.fromAscii) (String.toAscii >> Ok)
    let toAscii = SyncReversibleConverter.rev fromAscii

    let toByte =
        SyncReversibleConverter.createInstance (Result.catch Byte.parse) (Byte.toString >> Ok)
    let fromByte = SyncReversibleConverter.rev toByte

    let toInt32 =
        SyncReversibleConverter.createInstance (Result.catch Int32.parse) (Int32.toString >> Ok)
    let fromInt32 = SyncReversibleConverter.rev toInt32
