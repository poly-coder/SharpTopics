namespace SharpFunky

type AsyncResult<'t, 'e> = Async<Result<'t, 'e>>

module AsyncResult =
    open System.Threading.Tasks

    let ok v = Result.Ok v |> async.Return
    let error e = Result.Error e |> async.Return

    let matches f fe = Async.bind (Result.matches f fe)
    let matchesSync f fe = matches (f >> Async.return') (fe >> Async.return')
    
    let bind (f: _ -> AsyncResult<_, _>) = matches f (Error >> Async.return')
    let bindError (f: _ -> AsyncResult<_, _>) = matches ok f
    let map f = bind (f >> ok)
    let mapError f = bindError (f >> error)
    let ignoreAll ma = ma |> matchesSync ignore ignore
    let ignore ma = ma |> map ignore

    let catch fn: AsyncResult<_, _> = async {
        try
            let! a = fn()
            return! ok a
        with exn ->
            return! error exn
    }

    let ofAsync ma: AsyncResult<_, _> = async {
        try
            let! a = ma
            return Result.ok a
        with exn ->
            return Result.error exn
    }
    let ofTask ma = ma |> Async.AwaitTask |> ofAsync
    let ofTaskVoid (ma: Task) = ma |> Async.AwaitTask |> ofAsync
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

    module Builder =
        open System.Threading.Tasks

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
            member this.Return(a) = return' a
            member this.ReturnFrom ma = returnFrom ma
            //member this.ReturnFrom ma = returnFrom (ofResult ma)
            //member this.ReturnFrom (ma: Task<Result<_, _>>) = returnFrom (ofTask ma)

            member this.Bind(ma, f) = bind f ma
            //member this.Bind(ma, f) = bind (f >> ofResult) ma
            //member this.Bind(ma, f) = bind (f >> ofTask) ma

            member this.Combine(mu, mb: _ -> AsyncResult<_, _>) = this.Bind(mu, mb)
            //member this.Combine(mu, mb: _ -> Result<_, _>) = this.Bind(mu, mb)
            //member this.Combine(mu, mb: _ -> Task<_>) = this.Bind(mu, mb)

            member this.TryWith(ma, handler) = tryWith handler ma

            member this.TryFinally(ma, compensation) = tryFinally compensation ma

            member this.Using(res, body) = using body res

            member this.While(guard, body) = while' body guard

            member this.For(sequence, body) = for' body sequence

    let asyncResult = Builder.ResultBuilder()

[<AutoOpen>]
module AsyncResultBuilders =
    let asyncResult = AsyncResult.asyncResult