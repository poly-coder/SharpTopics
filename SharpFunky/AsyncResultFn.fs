namespace SharpFunky

type AsyncResultFn<'a, 'b, 'e> = 'a -> AsyncResult<'b, 'e>

module AsyncResultFn =
    let ok v = fun _ -> AsyncResult.ok v
    let error e = fun _ -> AsyncResult.error e

    let inline internal applyFn (f: AsyncResultFn<_, _, _>) fma: AsyncResultFn<_, _, _> = fma >> f

    let matches f fe (mab: AsyncResultFn<_, _, _>) = mab >> Async.bind (Result.matches f fe)
    let matchesSync f fe = matches (f >> Async.return') (fe >> Async.return')
    let matches' f fe (mab: AsyncResultFn<_, _, _>) = mab |> applyFn (AsyncResult.matches f fe)
    
    let bind f = applyFn (AsyncResult.bind f)
    let bindError f = applyFn (AsyncResult.bindError f)
    let map f = applyFn (AsyncResult.map f)
    let mapError f = applyFn (AsyncResult.mapError f)

    let ofAsync ma = ma |> applyFn AsyncResult.ofAsync
    let ofTask ma = ma |> applyFn AsyncResult.ofTask
    let ofResult ma = ma |> applyFn AsyncResult.ofResult
    let ofFn ma = ma |> applyFn AsyncResult.ok

    let bindAsync f = ofAsync >> bind f
    let mapAsync f = ofAsync >> map f
    let bindTask f = ofTask >> bind f
    let mapTask f = ofTask >> map f
    let bindResult f = ofResult >> bind f
    let mapResult f = ofResult >> map f

    let getOrExn ma = ma |> Monads.applyFn AsyncResult.getOrExn
    let getOrFail ma = ma |> Monads.applyFn AsyncResult.getOrFail
