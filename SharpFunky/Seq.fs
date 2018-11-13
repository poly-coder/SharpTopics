module SharpFunky.Seq
open System.Collections.Generic

type OpScanWith<'a> =
    | Continue
    | ContinueWith of 'a
    | Break
    | BreakWith of 'a

let bind f = Seq.collect f
let ignore ma = Seq.map ignore ma

let enumerator (e: seq<'a>) = e.GetEnumerator()
let current (e: IEnumerator<_>) = e.Current
let moveNext (e: IEnumerator<_>) = e.MoveNext()

let tee f = Seq.map (tee f)

let zipShorter l1 l2 =
    let e1 = enumerator l1
    let e2 = enumerator l2
    let rec loop m1 m2 = seq {
        if m1 && m2 then
            yield current e1, current e2
        else
            yield! loop (moveNext e1) (moveNext e2)
    }
    loop (moveNext e1) (moveNext e2)

let slice from count xs =
    let e = enumerator xs
    let rec loop pos moved = seq {
        if moved then
            if pos < from then
                yield! loop (pos + 1) (moveNext e)
            elif count < 0 || pos < from + count then
                yield current e
                yield! loop (pos + 1) (moveNext e)
    }
    loop 0 (moveNext e)

let scanWith folder state source =
    let e = enumerator source
    let rec loop state moved = seq {
        if moved then
            match folder state (current e) with
            | ContinueWith state' ->
                yield state'
                yield! loop state' (moveNext e)
            | Continue ->
                yield! loop state (moveNext e)
            | BreakWith state' ->
                yield state'
            | Break ->
                do ()
    }
    seq {
        yield state
        yield! loop state (moveNext e)
    }

let scanOptionSkip folder state source =
    let mapper = function Some x -> ContinueWith x | _ -> Continue
    scanWith (fun s a -> folder s a |> mapper) state source

let scanOptionBreak folder state source =
    let mapper = function Some x -> ContinueWith x | _ -> Break
    scanWith (fun s a -> folder s a |> mapper) state source

let scanPredSkip folder pred state source =
    let mapper = function s when pred s -> ContinueWith s | _ -> Continue
    scanWith (fun s a -> folder s a |> mapper) state source

let scanPredBreak folder pred state source =
    let mapper = function s when pred s -> ContinueWith s | _ -> Break
    scanWith (fun s a -> folder s a |> mapper) state source

let foldWith folder state =
    scanWith folder state >> Seq.last
let foldOptionSkip folder state =
    scanOptionSkip folder state >> Seq.last
let foldOptionBreak folder state =
    scanOptionBreak folder state >> Seq.last
let foldPredSkip folder pred state =
    scanPredSkip folder pred state >> Seq.last
let foldPredBreak folder pred state =
    scanPredBreak folder pred state >> Seq.last

module Infix =
    let inline (>>=) g f = g |> bind f
    let inline (>>-) g f = g |> Seq.map f
    let inline (>>~) g f = g |> Seq.filter f
