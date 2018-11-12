namespace SharpTopics.Core

type IKeyValueStore<'k, 't> =
    abstract get: 'k -> AsyncResult<'t option, exn>
    abstract put: 'k -> 't -> AsyncResult<unit, exn>
    abstract del: 'k -> AsyncResult<unit, exn>

module KeyValueStore =
    open Newtonsoft.Json.Schema

    type Options<'k, 't> = {
        validateKey: 'k -> AsyncResult<unit, exn>
        validateValue: 't -> AsyncResult<unit, exn>
        get: 'k -> AsyncResult<'t option, exn>
        put: 'k -> 't -> AsyncResult<unit, exn>
        del: 'k -> AsyncResult<unit, exn>
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

