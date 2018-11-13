namespace SharpFunky

module Option =

    let matches fs fn = function Some a -> fs a | None -> fn ()
    let firstSome xs =
        (None, xs)
        ||> Seq.foldWith (fun _ -> function
            | Some _ as a -> Seq.BreakWith a
            | _ -> Seq.Continue)

    let catch f x = try f x |> Some with _ -> None
    let filter f = Option.bind (fun x -> if f x then Some x else None)
    let ignore ma = Option.map ignore ma

    let toSeq ma = matches Seq.singleton (konst Seq.empty) ma
    let ofPredicate predicate v = if predicate v then Some v else None
    let ofTryOp (ok, v) = if ok then Some v else None

    module Infix =
        let inline (>>=) ma f = ma |> Option.bind f
        let inline (>>-) ma f = ma |> Option.map f
        let inline (>>~) ma f = ma |> Option.filter f

    module Maybe =
        let zero = Some ()
        let inline return' a = Some a
        let inline returnFrom ma = ma
        let inline delay f = Monads.delay f
        let inline run f = Monads.run f
        let inline tryWith handler ma = Monads.tryWith returnFrom handler ma
        let inline tryFinally compensation ma = Monads.tryFinally returnFrom compensation ma
        let inline using body res = Monads.using tryFinally body res
        let inline while' body guard = Monads.while' zero body guard
        let inline for' body sequence = Monads.for' using while' delay body sequence

        type MaybeBuilder() =
            member this.Delay f = delay f
            member this.Run f = run f

            member this.Zero() = zero
            member this.Return(a) = return' a
            member this.ReturnFrom ma = returnFrom ma
            member this.Bind(ma, f) = Option.bind f ma

            member this.Combine(mu, mb) = Option.bind mb mu

            member this.TryWith(ma, handler) = tryWith handler ma

            member this.TryFinally(ma, compensation) = tryFinally compensation ma

            member this.Using(res, body) = using body res

            member this.While(guard, body) = while' body guard

            member this.For(sequence, body) = for' body sequence

    let maybe = Maybe.MaybeBuilder()

    module OrElse =
        let zero = None
        let inline return' a = Some a
        let inline returnFrom ma = ma
        let inline some a = Some a
        let inline bind f = matches some f
        let inline map f = bind (f >> some)
        let inline delay f = Monads.delay f
        let inline run f = Monads.run f
        let inline tryWith handler ma = Monads.tryWith returnFrom handler ma
        let inline tryFinally compensation ma = Monads.tryFinally returnFrom compensation ma
        let inline using body res = Monads.using tryFinally body res
        let inline while' body guard = Monads.while' zero body guard
        let inline for' body sequence = Monads.for' using while' delay body sequence

        module Infix =
            let inline (>>=) ma f = ma |> bind f
            let inline (>>-) ma f = ma |> map f

        type OrElseBuilder() =
            member this.Delay f = delay f
            member this.Run f = run f

            member this.Zero() = zero
            member this.Return(a) = return' a
            member this.ReturnFrom ma = returnFrom ma
            member this.Bind(ma, f) = bind f ma

            member this.Combine(mu, mb) = bind mb mu

            member this.TryWith(ma, handler) = tryWith handler ma

            member this.TryFinally(ma, compensation) = tryFinally compensation ma

            member this.Using(res, body) = using body res

            member this.While(guard, body) = while' body guard

            member this.For(sequence, body) = for' body sequence

    let orElse = OrElse.OrElseBuilder()

[<AutoOpen>]
module OptionBuilders =
    let maybe = Option.maybe
    let orElse = Option.orElse
