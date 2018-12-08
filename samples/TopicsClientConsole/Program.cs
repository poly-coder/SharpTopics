using Google.Protobuf;
using Grpc.Core;
using System;
using System.Threading.Tasks;
using Topics.Protocols;

namespace TopicsClientConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Test1().GetAwaiter().GetResult();

            Console.WriteLine("Press a key to continue...");
            Console.ReadKey();
        }

        private static async Task Test1()
        {
            Channel channel = new Channel("localhost:8090", ChannelCredentials.Insecure);
            var client = new TopicService.TopicServiceClient(channel);

            var request = new PublishProtoMessage
            {
                MessageId = "123456",
                Data = ByteString.CopyFromUtf8("Hello world"),
                Meta =
                {
                    { "Encoding", "utf-8" }
                }

            };
            Console.WriteLine("Request: {0}", request);
            // var response = await client.PublishMessageAsync(request);
            // Console.WriteLine("Response: {0}", response);
            channel.ShutdownAsync().Wait();
        }
    }
}
