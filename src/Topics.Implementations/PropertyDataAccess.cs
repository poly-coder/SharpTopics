using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Topics.Abstractions;

namespace Topics.Implementations
{
    // Not thread safe. Use inside a sequential manager (MailboxProcessor, Actor, etc.)
    public class MongoPropertyDataAccess : IPropertyDataAccess
    {
        private IMongoCollection<BsonDocument> collection;

        private FilterDefinitionBuilder<BsonDocument> filter;
        private SortDefinitionBuilder<BsonDocument> sorter;
        private State state;

        public string PropertyId { get; }

        public MongoPropertyDataAccess(IMongoCollection<BsonDocument> collection, string propertyName)
        {
            this.collection = collection;
            this.PropertyId = propertyName;
            this.filter = Builders<BsonDocument>.Filter;
            this.sorter = Builders<BsonDocument>.Sort;
        }

        public async Task<CreatePropertyResponse> Create(CancellationToken cancellationToken = default(CancellationToken))
        {
            await EnsureLoaded(cancellationToken);
            var error = state.Create(PropertyId);
        }

        public async Task<bool> Exists(CancellationToken cancellationToken = default(CancellationToken))
        {
            await EnsureLoaded(cancellationToken);
            return state.Id != null;
        }

        private async Task EnsureLoaded(CancellationToken cancellationToken)
        {
            if (this.state == null)
            {
                var session = await collection.Database.Client.StartSessionAsync();
                try
                {
                    var state = new State();
                    var allEvents = await collection
                        .FindAsync(
                            session,
                            filter.Eq("aggregateId", PropertyId) &
                            filter.Eq("aggregateType", "property"),
                            new FindOptions<BsonDocument, BsonDocument>()
                            {
                                Sort = sorter.Ascending("version")
                            });
                    await allEvents.ForEachAsync((ev) =>
                    {
                        state.ApplyEvent(ev);
                    }, cancellationToken);

                    await session.CommitTransactionAsync();
                    this.state = state;
                }
                catch (Exception)
                {
                    await session.AbortTransactionAsync();
                }
            }
        }

        class State
        {
            public string Id { get; private set; }
            public string Name { get; private set; }
            public int Version { get; private set; }

            public List<BsonDocument> PendingEvents { get; set; }

            public void ApplyEvent(BsonDocument ev)
            {
                this.Version = ev["version"].AsInt32;
                switch (ev["eventType"].AsString)
                {
                    case "property-created":
                        this.Id = ev["aggregateId"].AsString;
                        break;

                    case "property-name-updated":
                        this.Name = ev["name"].AsString;
                        break;

                    default:
                        break;
                }
            }

            private ErrorInfo CreateCommand(string id, Func<IEnumerable<BsonDocument>> getEvents)
            {
                if (Id != null)
                {
                    return new ErrorInfo
                    {
                        Code = "EXISTS",
                        Message = "Property already exists"
                    };
                }

                PendingEvents.AddRange(getEvents().Select(e =>
                {
                    e["aggregateId"] = id;
                    e["aggregateType"] = "property";
                    return e;
                }));

                return null;
            }

            public ErrorInfo Create(string propertyId)
            {
                IEnumerable<BsonDocument> events()
                {
                    yield return new BsonDocument { { "eventType", "property-created" } };
                }
                return CreateCommand(propertyId, events);
            }
        }
    }
}
