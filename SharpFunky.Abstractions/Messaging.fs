namespace SharpFunky.Messaging

open SharpFunky
open System

type IMessagePublisher<'m, 'r> =
    abstract publish: 'm -> AsyncResult<'r, exn>

type IMessageSubscriber<'m> =
    abstract subscribe: AsyncFn<'m, unit> -> IDisposable

module MessagePublisher =

    let createInstance publish =
        { new IMessagePublisher<'m, 'r> with
            member __.publish message = publish message }

    let toFun (converter: IMessagePublisher<'a, 'b>) =
        fun a -> converter.publish a

    let convertInput converter (publisher: IMessagePublisher<'b, 'r>) =
        converter |> AsyncResultFn.bind (toFun publisher) |> createInstance

    let convertOutput converter (publisher: IMessagePublisher<'b, 'r>) =
        toFun publisher |> AsyncResultFn.bind converter |> createInstance

module MessageSubscriber =

    let createInstance subscribe =
        { new IMessageSubscriber<'m> with
            member __.subscribe handler = subscribe handler }
