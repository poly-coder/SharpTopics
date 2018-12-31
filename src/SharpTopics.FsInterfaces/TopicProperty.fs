namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans
open SharpTopics.Core
open System

type TopicPropertyInitializer = {
    identifier: Guid
    name: string
    normalizedName: string
}

type ITopicPropertyObserver =
    inherit IGrainObserver

type ITopicPropertyManagerObserver =
    inherit IGrainObserver

type ITopicProperty =
    inherit IGrainWithGuidKey

    abstract Initialize: request: TopicPropertyInitializer -> Task<string>

    abstract Subscribe: observer: ITopicPropertyObserver -> Task<unit>
    abstract Unsubscribe: observer: ITopicPropertyObserver -> Task<unit>

type ITopicPropertyManager =
    inherit IGrainWithStringKey

    abstract FindPropertyByName: name: string -> Task<TopicPropertyInitializer option>
    abstract CreateProperty: name: string -> Task<TopicPropertyInitializer option>

    abstract Subscribe: observer: ITopicPropertyManagerObserver -> Task<unit>
    abstract Unsubscribe: observer: ITopicPropertyManagerObserver -> Task<unit>
