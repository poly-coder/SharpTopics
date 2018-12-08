using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Topics.Abstractions
{
    public interface IPropertyCollectionDataAccess
    {
        Task<ListPropertiesResponse> ListProperties(CancellationToken cancellationToken = default(CancellationToken));
        Task<IPropertyDataAccess> GetPropertyDataAccess(string propertyName, CancellationToken cancellationToken = default(CancellationToken));
    }

    public interface IPropertyDataAccess
    {
        string PropertyId { get; }

        Task<bool> Exists(CancellationToken cancellationToken = default(CancellationToken));
        Task<CreatePropertyResponse> Create(CancellationToken cancellationToken = default(CancellationToken));
    }

    public class ListPropertiesResponse
    {
        public IEnumerable<PropertyItemInfo> Items { get; set; }
        public ErrorInfo Error { get; set; }
    }

    public class PropertyItemInfo
    {
        public string Id { get; set; }
    }

    public class CreatePropertyRequest
    {
        public string Id { get; set; }
    }

    public class CreatePropertyResponse
    {
        public ErrorInfo Error { get; set; }
    }
}
