namespace SharpFunky

type AsyncFn<'a, 'b> = 'a -> Async<'b>
type AsyncMiddleware<'a, 'b> = AsyncFn<'a, 'b> -> AsyncFn<'a, 'b>

module AsyncFn =
    let return' v = fun _ -> Async.return' v
    let raise e = fun _ -> Async.raise e

    let inline internal applyFn (f: AsyncFn<_, _>) fma: AsyncFn<_, _> = fma >> f
    
    let bind f = applyFn (Async.bind f)
    let map f = applyFn (Async.map f)
    let ignore ma = ma |> map ignore

    let ofTask ma = ma |> applyFn Async.ofTask
    let ofTaskVoid ma = ma |> applyFn Async.ofTaskVoid
    let toTask ma = ma |> Monads.applyFn Async.toTask
    let toTaskVoid ma = ma |> Monads.applyFn Async.toTaskVoid

    let bindTask f = applyFn (Async.bindTask f)
    let bindTaskVoid f = applyFn (Async.bindTaskVoid f)
    let mapTask f = applyFn (Async.mapTask f)
    let mapTaskVoid f = applyFn (Async.mapTaskVoid f)
