namespace SharpFunky

type Fn<'a, 'b> = 'a -> 'b

module Fn =
    let id : Fn<_, _> = fun a -> a

type Middleware<'a, 'b> = Fn<'a, 'b> -> Fn<'a, 'b>

module Middleware =
    //let pipe m1 m2 : Middleware<_, _> = fun fn ->
    //    fun a ->

    let create beforeAction afterAction : Middleware<_, _> = fun fn ->
        fun a ->
            let data = beforeAction a
            let b = fn a
            do afterAction a b data
            b

    let before action = create action (fun _ _ -> id)
    let after action = create ignore (fun a b _ -> action a b)
    let afterOut action = create ignore (fun _ b _ -> action b)
