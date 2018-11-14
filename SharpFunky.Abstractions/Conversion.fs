namespace SharpFunky.Conversion

open SharpFunky

type IConverter<'a, 'b> =
    abstract convert: 'a -> AsyncResult<'b, exn>

type IReversibleConverter<'a, 'b> =
    inherit IConverter<'a, 'b>
    abstract convertBack: 'b -> AsyncResult<'a, exn>

module Converter =
    let createInstance convert =
        { new IConverter<'a, 'b> with
            member __.convert a = convert a }

    let toFun (converter: IConverter<'a, 'b>) =
        fun a -> converter.convert a

    let compose cab cbc =
        toFun cab |> AsyncResultFn.bind (toFun cbc) |> createInstance

module ReversibleConverter =
    let createInstance convert convertBack =
        { new IReversibleConverter<'a, 'b> with
            member __.convert a = convert a
            member __.convertBack b = convertBack b }

    let toFun (converter: IReversibleConverter<'a, 'b>) =
        fun a -> converter.convert a

    let toFunBack (converter: IReversibleConverter<'a, 'b>) =
        fun a -> converter.convertBack a

    let compose cab cbc =
        let convert = toFun cab |> AsyncResultFn.bind (toFun cbc)
        let convertBack = toFunBack cbc |> AsyncResultFn.bind (toFunBack cab)
        createInstance convert convertBack
