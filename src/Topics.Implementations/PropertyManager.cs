using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Threading.Tasks;
using Topics.Abstractions;

namespace Topics.Implementations
{
    public class MongoPropertyManager : IPropertyManager
    {
        private IMongoCollection<BsonDocument> collection;

        public MongoPropertyManager(IMongoCollection<BsonDocument> collection)
        {
            this.collection = collection;
        }

        public Task<CreatePropertyResponse> CreateProperty(CreatePropertyRequest request)
        {
            throw new NotImplementedException();
        }

        public Task<ListPropertiesResponse> ListProperties()
        {
            throw new NotImplementedException();
        }
    }
}
