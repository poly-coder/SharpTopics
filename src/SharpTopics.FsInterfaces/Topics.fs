namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans

type IMessagePublisher =
    inherit IGrainWithStringKey

    abstract PublishMessage: string -> Task<string>
