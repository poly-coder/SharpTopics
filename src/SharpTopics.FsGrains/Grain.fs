namespace SharpTopics.FsGrains

open Orleans
open SharpTopics.FsInterfaces
open FSharp.Control.Tasks.V2

type HelloGrain() =
    inherit Grain()
    
    interface IHello with
        member this.SayHello (greeting: string) = task {
            return sprintf "You said: %s, I say Hello!" greeting
        }