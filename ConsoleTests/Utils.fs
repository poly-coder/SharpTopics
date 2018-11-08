module Utils

type SetOp<'a, 'b> =
    | AddFromSource of 'a
    | RemoveFromDestination of 'b
    | UpdateInPlace of 'a * 'b

let diffSets same source dest =
    let notInDest a = dest |> Seq.exists (fun b -> same a b) |> not
    let restMapper = function 
        | Some a, b -> UpdateInPlace(a, b)
        | None, b -> RemoveFromDestination b
    let adds =
        seq {
            for a in source do
            if notInDest a then yield a 
        } |> Seq.map AddFromSource
    let rest =
        seq {
            for b in dest do
                let maybeA = source |> Seq.tryFind (fun a -> same a b)
                yield maybeA, b
        } |> Seq.map restMapper
    Seq.concat [adds; rest]

let updateSet same create replace source dest =
    let ops = diffSets same source dest
    (dest, ops)
    ||> Seq.fold (fun bs -> function
        | AddFromSource a -> bs |> Set.add (create a)
        | RemoveFromDestination b ->  bs |> Set.remove b
        | UpdateInPlace(a, b) -> bs |> Set.remove b |> Set.add (replace a b))
