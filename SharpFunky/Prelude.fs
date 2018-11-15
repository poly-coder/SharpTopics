[<AutoOpen>]
module SharpFunky.Prelude

open System

let konst c = fun _ -> c
let konst2 c = fun _ _ -> c
let konst3 c = fun _ _ _ -> c
let tee f v = f v; v
let flip f = fun a b -> f b a
let fstArg a _ = a
let sndArg _ a = a

let toObj a = a :> obj
let fromObj<'a> (o: obj) = o :?> 'a
let objRefEq a b = obj.ReferenceEquals(a, b)
let disposeOf (e: #System.IDisposable) =
    match e with null -> () | e -> e.Dispose()
let isNotNull o = isNull o |> not

let append xs lst = lst @ xs
let prepend xs lst = xs @ lst

let inline eq x = fun y -> y = x
let inline lt x = fun y -> y < x
let inline le x = fun y -> y <= x
let inline gt x = fun y -> y > x
let inline ge x = fun y -> y >= x
let inline neq x = eq x >> not
let inline nlt x = lt x >> not
let inline nle x = le x >> not
let inline ngt x = gt x >> not
let inline nge x = ge x >> not

// compose operator (>>) with more than one parameter in the first operand
// let inline (>*>) f g = fun a b -> f a b |> g
// let inline (>**>) f g = fun a b c -> f a b c |> g

let curry f = fun a b -> f(a, b)
let uncurry f = fun (a, b) -> f a b

let notImpl = fun _ -> raise <| System.NotImplementedException()
let notImpl2 = fun _ _ -> raise <| System.NotImplementedException()
let notImpl3 = fun _ _ _ -> raise <| System.NotImplementedException()

type BinarySearchResult =
  | ItShouldHaveBeenAt of int
  | ItIsAt of int