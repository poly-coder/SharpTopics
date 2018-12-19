namespace SharpTopics.FsInterfaces

open System.Threading.Tasks
open Orleans

type IMessageReaderObserver =
    inherit IGrainObserver

    abstract AcceptMessages: messages: MessageListResult -> unit

type IMessageReader =
    inherit IGrainWithStringKey

    abstract StartFromSequence: sequence: int64 -> Task<unit>

    abstract IssueQuota: count: int -> Task<unit>

    abstract Subscribe: observer: IMessageReaderObserver -> Task<unit>
    abstract Unsubscribe: observer: IMessageReaderObserver -> Task<unit>
