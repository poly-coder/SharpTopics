namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans
open SharpTopics.Core

type IMessagePublisher =
    inherit IGrainWithStringKey

    abstract PublishMessages: Message seq -> Task<MessageMeta seq>
