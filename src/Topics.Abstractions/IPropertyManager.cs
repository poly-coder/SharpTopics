using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Topics.Abstractions
{
    public interface IPropertyManager
    {
        Task<ListPropertiesResponse> ListProperties();
        Task<CreatePropertyResponse> CreateProperty(CreatePropertyRequest request);
    }

    public class ErrorInfo
    {
        public string Message { get; set; }
        public string Code { get; set; }
    }

    public class ListPropertiesResponse
    {
        public IEnumerable<PropertyItemInfo> Items { get; set; }
        public ErrorInfo Error { get; set; }
    }

    public class PropertyItemInfo
    {
        public string Name { get; set; }
    }

    public class CreatePropertyRequest
    {
        public string Name { get; set; }
    }

    public class CreatePropertyResponse
    {
        public ErrorInfo Error { get; set; }
    }
}
