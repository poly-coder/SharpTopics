namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans

type IHello =
    inherit IGrainWithIntegerKey

    abstract SayHello: string -> Task<string>
