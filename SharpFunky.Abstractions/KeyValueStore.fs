namespace SharpFunky.Storage

open SharpFunky

type IKeyValueStore<'k, 't> =
    abstract get: 'k -> AsyncResult<'t option, exn>
    abstract put: 'k -> 't -> AsyncResult<'t, exn>
    abstract del: 'k -> AsyncResult<unit, exn>

module KeyValueStore =
    open System

    let createInstance get put del =
        { new IKeyValueStore<'k, 't> with
            member __.get key = get key
            member __.put key value = put key value
            member __.del key = del key
        }

    let empty () =
        createInstance
            (fun _ -> AsyncResult.ok None)
            (fun _ _ -> AsyncResult.error (NotSupportedException("") :> _))
            (fun _ -> AsyncResult.ok())

    [<RequireQualifiedAccess>]
    module InMemory =
        type Options<'k, 't when 'k: comparison> = {
            initMap: Map<'k, 't>
            updateValue: 'k -> 't -> 't
        }

        [<RequireQualifiedAccess>]
        module Options =
            let empty<'k, 't when 'k: comparison> : Options<'k, 't> = 
                {
                    initMap = Map.empty
                    updateValue = fun _ v -> v
                }
            let withInitMap value = fun opts -> { opts with initMap = value }
            let withUpdateValue value = fun opts -> { opts with updateValue = value }

        type internal Command<'k, 't> =
        | GetCmd of 'k * AsyncReplyChannel<AsyncResult<'t option, exn>>
        | PutCmd of 'k * 't * AsyncReplyChannel<AsyncResult<'t, exn>>
        | DelCmd of 'k * AsyncReplyChannel<AsyncResult<unit, exn>>

        let fromOptions opts =
            let updateValue = opts.updateValue
            let mailbox = MailboxProcessor.Start(fun mb -> 
                let rec loop map = async {
                    let! msg = mb.Receive()
                    match msg with
                    | GetCmd (key, rep) ->
                        let found = map |> Map.tryFind key
                        do rep.Reply <| AsyncResult.ok found
                        return! loop map

                    | PutCmd (key, value, rep) ->
                        let value' = updateValue key value
                        let map' = map |> Map.add key value'
                        do rep.Reply <| AsyncResult.ok value'
                        return! loop map'

                    | DelCmd (key, rep) ->
                        let map' = map |> Map.remove key
                        do rep.Reply <| AsyncResult.ok ()
                        return! loop map'
                }
                loop opts.initMap
            )
            let send f = async {
                let! res = mailbox.PostAndAsyncReply(f)
                return! res
            }
            let get key = send <| fun rep -> GetCmd(key, rep)
            let put key value = send <| fun rep -> PutCmd(key, value, rep)
            let del key = send <| fun rep -> DelCmd(key, rep)
            createInstance get put del

        let from map = Options.empty |> Options.withInitMap map |> fromOptions 
        
        let empty() = from Map.empty

    [<RequireQualifiedAccess>]
    module Validated =
        type Options<'k, 't when 'k: comparison> = {
            validateKey: 'k -> AsyncResult<unit, exn>
            validateValue: 't -> AsyncResult<unit, exn>
            store: IKeyValueStore<'k, 't>
        }

        [<RequireQualifiedAccess>]
        module Options =
            let fromStore store = 
                {
                    store = store
                    validateKey = fun _ -> AsyncResult.ok ()
                    validateValue = fun _ -> AsyncResult.ok ()
                }
            let withValidateKey value = fun opts -> { opts with validateKey = value }
            let withValidateValue value = fun opts -> { opts with validateValue = value }
        
        let fromOptions opts =
            let get key = asyncResult {
                do! opts.validateKey key
                return! opts.store.get key
            }
            let put key value = asyncResult {
                do! opts.validateKey key
                do! opts.validateValue value
                return! opts.store.put key value
            }
            let del key = asyncResult {
                do! opts.validateKey key
                return! opts.store.del key
            }
            createInstance get put del
