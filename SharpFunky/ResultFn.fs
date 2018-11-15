namespace SharpFunky

type ResultFn<'a, 'b, 'e> = 'a -> Result<'b, 'e>

module ResultFn =
    let ok v = fun _ -> Result.ok v
    let error e = fun _ -> Result.error e

    let inline internal applyFn (f: ResultFn<_, _, _>) fma: ResultFn<_, _, _> = fma >> f
    
    let bind f = applyFn (Result.bind f)
    let bindError f = applyFn (Result.bindError f)
    let map f = applyFn (Result.map f)
    let mapError f = applyFn (Result.mapError f)


