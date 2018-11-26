namespace SharpFunky

type Trial<'a, 'e> = 
    | Success of 'a * 'e list
    | Failure of 'e list

module Trial =
    let warns es a = Success(a, es)
    let warnsSeq es a = warns (es |> List.ofSeq) a
    let warn e a = warns [e] a
    let failures es: Trial<_, _> = Failure es
    let failuresSeq es = failures (es |> List.ofSeq)
    let failure e = failures [e]
    let success a = warns [] a

    let matches fWarns fFailures (ma: Trial<_, _>) = 
        match ma with
        | Success (a, es) -> fWarns es a
        | Failure es -> fFailures es
    
    let bind f = matches (fun _ -> f) failures
    let map f = bind (f >> success)
    let mapWarns f = matches (f >> warns) failures
    let mapWarn f = mapWarns (List.map f)
    let mapErrors f = matches warns (f >> failures)
    let mapError f = mapErrors (List.map f)
    let mapMessages f = matches (f >> warns) (f >> failures)
    let mapMessage f = mapMessages (List.map f)

    let warnsAsErrors ma =
        matches 
            (fun es a -> match es with [] -> success a | _ -> failures es)
            failures
            ma

    let catch fn x = try fn x |> success with exn -> failure exn

    let ofResult = function Ok a -> success a | Error e -> failure e
    let toResult = function Success(a, _) -> Ok a | Failure es -> Ok es

    //let ofOption = function Some a -> ok a | _ -> error ()
    //let ofOptionError = function Some a -> error a | _ -> ok ()
    //let toOption = function Ok a -> Some a | _ -> None
    //let toOptionError = function Error a -> Some a | _ -> None

    //let ofChoice = function Choice1Of2 a -> ok a | Choice2Of2 e -> error e
    //let toChoice = function Ok a -> Choice1Of2 a | Error e -> Choice2Of2 e

    //let toList = function Ok a -> [a] | _ -> []
    //let toListError = function Error a -> [a] | _ -> []
