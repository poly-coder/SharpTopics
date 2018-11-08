namespace SharpTopics.Core

type IKeyValueStore<'k, 't> =
    abstract get: 'k -> Async<Result<'t option, exn>>
    abstract put: 'k -> 't -> Async<Result<unit, exn>>
    abstract del: 'k -> Async<Result<unit, exn>>

module KeyValueStore =
    open Newtonsoft.Json.Schema

    type Options<'k, 't> = {
        validateKey: 'k -> Async<Result<unit, exn>>
        validateValue: 't -> Async<Result<unit, exn>>
        get: 'k -> Async<Result<'t option, exn>>
        put: 'k -> 't -> Async<Result<unit, exn>>
        del: 'k -> Async<Result<unit, exn>>
    }

    let makeKeyValueStore (opts: Options<'k, 't>) =
        let get key = async {
            match! opts.validateKey key with
            | Error exn -> return Error exn
            | Ok () -> return! opts.get key
        }

        let put key value = async {
            match! opts.validateKey key with
            | Error exn -> return Error exn
            | Ok () ->
                match! opts.validateValue value with
                | Error exn -> return Error exn
                | Ok () -> return! opts.put key value
        }

        let del key = async {
            match! opts.validateKey key with
            | Error exn -> return Error exn
            | Ok () -> return! opts.del key
        }

        { new IKeyValueStore<'k, 't> with
            member __.get key = get key
            member __.put key value = put key value
            member __.del key = del key
        }

