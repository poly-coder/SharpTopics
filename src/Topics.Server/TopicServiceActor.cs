using Akka.Actor;
using Akka.Event;
using Grpc.Core;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Topics.Protocols;

namespace Topics.Server
{
    public class TopicServiceOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public SslCredentialsOptions SslCredentials { get; set; }
    }

    public class SslCredentialsOptions
    {
        public List<KeyCertificateOptions> KeyCertificates { get; set; } = new List<KeyCertificateOptions>();
        public string RootCertificates { get; set; }
        public SslClientCertificateRequestType ClientCertificateRequest { get; set; }
    }

    public class KeyCertificateOptions
    {
        public string CertificateChain { get; set; }
        public string PrivateKey { get; set; }
    }

    public class TopicServiceActor : UntypedActor
    {
        private Grpc.Core.Server server;
        private TopicServiceOptions options;
        private ILoggingAdapter log;

        public TopicServiceActor(TopicServiceOptions options)
        {
            this.options = options;
            this.log = Logging.GetLogger(Context);
        }

        protected override void PreStart()
        {
            var credentials = ServerCredentials.Insecure;
            if (options.SslCredentials != null)
            {
                var pairs = options.SslCredentials.KeyCertificates
                    .Select(e => new KeyCertificatePair(e.CertificateChain, e.PrivateKey));
                if (options.SslCredentials.RootCertificates == null)
                {
                    credentials = new SslServerCredentials(pairs);
                }
                else
                {
                    credentials = new SslServerCredentials(pairs, 
                        options.SslCredentials.RootCertificates,
                        options.SslCredentials.ClientCertificateRequest);
                }
                
            }

            server = new Grpc.Core.Server()
            {
                Ports =
                {
                    new ServerPort(options.Host, options.Port, credentials),
                },
                Services =
                {
                    TopicService.BindService(new TopicServiceImpl())
                }
            };
            log.Info("Topic Service listening on {0}:{1}", options.Host, options.Port);
            server.Start();
        }

        protected override void PostStop()
        {
            ShutdownServer();
        }

        private void ShutdownServer()
        {
            if (server != null)
            {
                log.Info("Topic Service shutting down ...");
                server.ShutdownAsync();
                server = null;
            }
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                default:
                    break;
            }
        }
    }

    public class TopicServiceImpl: TopicService.TopicServiceBase
    {
        public override Task<CreatePropertyResponse> CreateProperty(CreatePropertyRequest request, ServerCallContext context)
        {
            return base.CreateProperty(request, context);
        }
    }
}
