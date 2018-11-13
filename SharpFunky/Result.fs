namespace SharpFunky

module Result =

    let ok v = Result.Ok v
    let error e = Result.Error e

    let matches f fe = function Ok v -> f v | Error e -> fe e
    
    let bind f = matches f Error
    let bindError f = matches Ok f
    let map f = bind (f >> Ok)
    let mapError f = bindError (f >> Error)

    let catch fn x = try fn x |> ok with exn -> error exn
    
    let ofOption = function Some a -> ok a | _ -> error ()
    let ofOptionError = function Some a -> error a | _ -> ok ()
    let toOption = function Ok a -> Some a | _ -> None
    let toOptionError = function Error a -> Some a | _ -> None

    let ofChoice = function Choice1Of2 a -> ok a | Choice2Of2 e -> error e
    let toChoice = function Ok a -> Choice1Of2 a | Error e -> Choice2Of2 e

    let toList = function Ok a -> [a] | _ -> []
    let toListError = function Error a -> [a] | _ -> []

    module Builder =
        let zero = Result.Ok()
        let inline return' a = ok a
        let inline returnFrom ma = ma
        let inline delay f = Monads.delay f
        let inline run f = Monads.run f
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
            member this.Bind(ma, f) = bind f ma

            member this.Combine(mu, mb) = bind mb mu

            member this.TryWith(ma, handler) = tryWith handler ma

            member this.TryFinally(ma, compensation) = tryFinally compensation ma

            member this.Using(res, body) = using body res

            member this.While(guard, body) = while' body guard

            member this.For(sequence, body) = for' body sequence

    let result = Builder.ResultBuilder()

    module ResultOr =
        let zero = Result.Error()
        let inline return' a = ok a
        let inline returnFrom ma = ma
        let inline bind f = matches ok f
        let inline map f = bind (f >> ok)
        let inline delay f = Monads.delay f
        let inline run f = Monads.run f
        let inline tryWith handler ma = Monads.tryWith returnFrom handler ma
        let inline tryFinally compensation ma = Monads.tryFinally returnFrom compensation ma
        let inline using body res = Monads.using tryFinally body res
        let inline while' body guard = Monads.while' zero body guard
        let inline for' body sequence = Monads.for' using while' delay body sequence

        type ResultOrBuilder() =
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

    let resultOr = ResultOr.ResultOrBuilder()

[<AutoOpen>]
module ResultBuilders =
    let result = Result.result
    let resultOr = Result.resultOr
