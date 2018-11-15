module SharpFunky.Monads

open System
open System.Collections.Generic

let inline delay f = f
let inline run f = f()
let inline delayNot f = f()
let inline runNot f = f

let tryWith returnFrom handler ma =
    try returnFrom ma
    with exn -> handler exn

let tryFinally returnFrom compensation ma =
    try returnFrom ma
    finally compensation ()

let using tryFinally body res =
    body res |> tryFinally (fun () -> disposeOf res)

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

let applyFn f fma = fma >> f
