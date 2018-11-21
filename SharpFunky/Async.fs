module SharpFunky.Async

open System.Threading.Tasks

let zero = async.Zero()
let return' a = async.Return a
let delay f = async.Delay f
let raise exn = delay <| fun () -> raise exn
let failwith msg = delay <| fun () -> failwith msg
let invalidArg name msg = delay <| fun () -> invalidArg name msg
let nullArg msg = delay <| fun () -> nullArg msg
let invalidOp msg = delay <| fun () -> invalidOp msg
let notImpl() = delay notImpl

let bind f ma = async.Bind(ma, f)
let map f = bind (f >> return')

let ofTask ma = Async.AwaitTask ma
let ofTaskVoid ma = Async.AwaitTask (ma: Task)
let toTask ma = Async.StartAsTask ma
let toTaskVoid ma = toTask ma |> fun t -> t :> Task

let inline startAsTask ma = ma |> Async.StartAsTask
let inline startAsTaskVoid ma = ma |> startAsTask |> fun t -> t :> Task

let bindTask f = ofTask >> bind f
let bindTaskVoid f = ofTaskVoid >> bind f
let mapTask f = ofTask >> map f
let mapTaskVoid f = ofTaskVoid >> map f

let ignoreExn ma = async {
    try do! ma with _ -> ()
}

let whenAllSerial source = async {
    let e = (source: _ seq).GetEnumerator()
    let rec loop l moved = async {
        if not moved then
            return List.rev l |> List.toSeq
        else
            let! value = e.Current
            return! loop (value :: l) (e.MoveNext())
    }
    return! loop [] (e.MoveNext())
}

let whenAll source =
    source
    |> Seq.map (Async.StartAsTask)
    |> Task.WhenAll
    |> Async.AwaitTask
    |> map Array.toSeq

let toPromise a =
    let value = ref None
    let locker = obj()
    async {
        match !value with
        | Some v -> return v
        | None ->
            return! lock locker <| fun () -> async {
                match !value with
                | Some v -> return v
                | None ->
                    let! v = a
                    value := Some v
                    return v
            }
    }

module Infix =
    let inline (>>=) ma f = ma |> bind f
    let inline (>>-) ma f = ma |> map f
