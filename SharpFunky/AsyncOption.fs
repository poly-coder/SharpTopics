namespace SharpFunky

type AsyncOption<'a> = Async<Option<'a>>

module AsyncOption =
    let some x : AsyncOption<'a> = Option.Some x |> async.Return
    let none<'a> : AsyncOption<'a> = Option.None |> async.Return
    let ofAsync (a: Async<'a>) : AsyncOption<'a> =
        Async.bind (Some >> async.Return) a
    let ofOption (a: Option<'a>) : AsyncOption<'a> =
        a |> async.Return

    let bind f ma = Async.bind (Option.matches f (konst none)) ma
    let bindOfAsync f ma = bind (f >> ofAsync) ma
    let bindOfOption f ma = bind (f >> ofOption) ma
    let map f ma = bind (f >> some) ma

    let filter (f: 'a -> Async<bool>) (ma: AsyncOption<'a>) : AsyncOption<'a> = async {
        let! a = ma
        let! cond = match a with Some x -> f x | _ -> Async.return' false
        if cond then return a else return None
    }
    let filterSync f ma = filter (f >> Async.return') ma

    module AsyncMaybe =
        let zero = some()
        let inline delay f = f
        let inline run f = f()
        let inline return' a = some a
        let inline returnFrom ma = ma
        let inline tryWith handler ma = Monads.tryWith returnFrom handler ma
        let inline tryFinally compensation ma = Monads.tryFinally returnFrom compensation ma
        let inline using body res = Monads.using tryFinally body res
        let inline while' body guard = Monads.while' zero body guard
        let inline for' body sequence = Monads.for' using while' delay body sequence

        type AsyncMaybeBuilder() =
            member this.Delay f = delay f
            member this.Run f = run f

            member this.Zero() = zero
            member this.Return(a) = return' a
            member this.ReturnFrom ma = returnFrom ma
            member this.Bind(ma, f) = bind f ma
            member this.Bind(ma, f) = bind (f >> ofOption) ma

            member this.Combine(mu, mb) = bind mb mu

            member this.TryWith(ma, handler) = tryWith handler ma

            member this.TryFinally(ma, compensation) = tryFinally compensation ma

            member this.Using(res, body) = using body res

            member this.While(guard, body) = while' body guard

            member this.For(sequence, body) = for' body sequence

    let asyncMaybe = AsyncMaybe.AsyncMaybeBuilder()

    module AsyncOrElse =
        let zero<'a> : AsyncOption<'a> = none
        let inline delay f = f
        let inline run f = f()
        let inline return' a = some a
        let inline returnFrom ma = ma
        let inline bind f ma = Async.bind (Option.matches some f) ma
        let inline map f ma = bind (f >> some) ma
        let inline tryWith handler ma = Monads.tryWith returnFrom handler ma
        let inline tryFinally compensation ma = Monads.tryFinally returnFrom compensation ma
        let inline using body res = Monads.using tryFinally body res
        let inline while' body guard = Monads.while' zero body guard
        let inline for' body sequence = Monads.for' using while' delay body sequence

        type AsyncOrElseBuilder() =
            member this.Delay f = delay f
            member this.Run f = run f

            member this.Zero() = zero
            member this.Return(a) = return' a
            member this.ReturnFrom ma = returnFrom ma
            member this.Bind(ma, f) = bind f ma
            member this.Bind(ma, f) = bind (f >> ofOption) ma

            member this.Combine(mu, mb) = bind mb mu

            member this.TryWith(ma, handler) = tryWith handler ma

            member this.TryFinally(ma, compensation) = tryFinally compensation ma

            member this.Using(res, body) = using body res

            member this.While(guard, body) = while' body guard

            member this.For(sequence, body) = for' body sequence

    let asyncOrElse = AsyncOrElse.AsyncOrElseBuilder()

[<AutoOpen>]
module AsyncOptionBuilders =
    let asyncMaybe = AsyncOption.asyncMaybe
    let asyncOrElse = AsyncOption.asyncOrElse

