namespace SharpTopics.Core
open System
open FSharp.Core

module Monads =
    open System.Collections.Generic

    let dispose (res: #IDisposable) =
        match res with null -> () | disp -> disp.Dispose()

    let tryWith returnFrom handler ma =
        try returnFrom ma
        with exn -> handler exn

    let tryFinally returnFrom compensation ma =
        try returnFrom ma
        finally compensation ()

    let using tryFinally body res =
        body res |> tryFinally (fun () -> dispose res)

    let while' zero body guard =
        let rec loop() =
            if guard() |> not then zero 
            else
                do body() |> ignore
                loop()
        loop()

    let for' using while' delay body (sequence: _ seq) =
        sequence.GetEnumerator() |> using (fun (enum: IEnumerator<_>) ->
            (fun () -> enum.MoveNext()) |> while' (delay (fun () -> 
                body enum.Current)))

module Result =

    let ok v = Result.Ok v
    let error e = Result.Error e

    let matches f fe = function Ok v -> f v | Error e -> fe e
    
    let bind f = matches f Error
    let bindError f = matches Ok f
    let map f = bind (f >> Ok)
    let mapError f = bindError (f >> Error)

    let catch fn = try fn() |> ok with exn -> error exn
    
    let ofOption = function Some a -> ok a | _ -> error ()
    let ofOptionError = function Some a -> error a | _ -> ok ()
    let toOption = function Ok a -> Some a | _ -> None
    let toOptionError = function Error a -> Some a | _ -> None

    let ofChoice = function Choice1Of2 a -> ok a | Choice2Of2 e -> error e
    let toChoice = function Ok a -> Choice1Of2 a | Error e -> Choice2Of2 e

    let toList = function Ok a -> [a] | _ -> []
    let toListError = function Error a -> [a] | _ -> []

    module Building =
        let zero = Result.Ok()
        let inline delay f = f
        let inline run f = f()
        let inline return' a = ok a
        let inline returnFrom ma = ma
        let inline tryWith handler ma = Monads.tryWith returnFrom handler ma
        let inline tryFinally compensation ma = Monads.tryFinally returnFrom compensation ma
        let inline using body res = Monads.using tryFinally body res
        let inline while' body guard = Monads.while' zero body guard
        let inline for' body sequence = Monads.for' using while' delay body sequence

        type ResultBuilder() =
            member this.Delay f = delay f
            member this.Run f = run f

            member this.Zero() = zero
            member this.Return(a) = ok a
            member this.ReturnFrom ma = ma
            member this.Bind(ma, f) = bind f ma

            member this.Combine(mu, mb) = bind mb mu

            member this.TryWith(ma, handler) = tryWith handler ma

            member this.TryFinally(ma, compensation) = tryFinally compensation ma

            member this.Using(res, body) = using body res

            member this.While(guard, body) = while' body guard

            member this.For(sequence, body) = for' body sequence

    let builder = Building.ResultBuilder()

module Async =

    let return' v = async.Return v
    let raise' e = async { return raise e }
    
    let bind f ma = async.Bind(ma, f)
    let map f = bind (f >> return')

type AsyncResult<'t, 'e> = Async<Result<'t, 'e>>

module AsyncResult =

    let ok v = Result.Ok v |> async.Return
    let error e = Result.Error e |> async.Return

    let matches f fe = fun ma -> async {
        match! ma with
        | Ok v -> return! f v 
        | Error e -> return! fe e
    }
    let matchesSync f fe = matches (f >> Async.return') (fe >> Async.return')
    
    let bind (f: _ -> AsyncResult<_, _>) = matches f (Error >> Async.return')
    let bindError (f: _ -> AsyncResult<_, _>) = matches ok f
    let map (f: _ -> AsyncResult<_, _>) = bind (f >> ok)
    let mapError (f: _ -> AsyncResult<_, _>) = bindError (f >> error)

    let catch fn: AsyncResult<_, _> = async {
        try
            let! a = fn()
            return! ok a
        with exn ->
            return! error exn
    }

    let ofAsync ma: AsyncResult<_, _> = ma |> Async.map Result.ok
    let ofResult ma: AsyncResult<_, _> = ma |> Async.return'
    
    let ofOption ma = ma |> Result.ofOption |> ofResult
    let ofOptionError ma = ma |> Result.ofOptionError |> ofResult

    let ofChoice ma = ma |> Result.ofChoice |> ofResult

    let getOrExn ma = async {
        match! ma with
        | Ok a -> return a
        | Error e -> return raise e
    }

    let getOrFail ma = async {
        match! ma with
        | Ok a -> return a
        | Error e -> return failwithf "Error: %A" e
    }

    module Building =
        let zero<'e> : AsyncResult<_, 'e> = ok()
        let inline delay f = f
        let inline run f = f()
        let inline return' a = ok a
        let inline returnFrom ma = ma
        let inline tryWith handler ma = Monads.tryWith returnFrom handler ma
        let inline tryFinally compensation ma = Monads.tryFinally returnFrom compensation ma
        let inline using body res = Monads.using tryFinally body res
        let inline while' body guard = Monads.while' zero body guard
        let inline for' body sequence = Monads.for' using while' delay body sequence

        type ResultBuilder() =
            member this.Delay f = delay f
            member this.Run f = run f

            member this.Zero() = zero
            member this.Return(a) = ok a
            member this.ReturnFrom ma = ma
            member this.Bind(ma, f) = bind f ma

            member this.Combine(mu, mb) = bind mb mu

            member this.TryWith(ma, handler) = tryWith handler ma

            member this.TryFinally(ma, compensation) = tryFinally compensation ma

            member this.Using(res, body) = using body res

            member this.While(guard, body) = while' body guard

            member this.For(sequence, body) = for' body sequence

    let builder = Building.ResultBuilder()

[<AutoOpen>]
module AsyncResultAutoOpen =
    let result = Result.builder
    let asyncResult = AsyncResult.builder
