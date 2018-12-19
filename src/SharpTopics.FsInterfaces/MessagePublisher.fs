namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans
open SharpTopics.Core

type IMessagePublisherObserver =
    inherit IGrainObserver

    abstract MessagesPublished: empty: unit -> unit

type IMessagePublisher =
    inherit IGrainWithStringKey

    abstract PublishMessages: messages: Message list -> Task<MessageMeta list>

    abstract Subscribe: observer: IMessagePublisherObserver -> Task<unit>
    abstract Unsubscribe: observer: IMessagePublisherObserver -> Task<unit>
