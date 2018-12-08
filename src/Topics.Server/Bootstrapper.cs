using System;
using Akka;
using Akka.Actor;
using Akka.DI.Core;
using Akka.Pattern;

namespace Topics.Server
{
    public class Bootstrapper
    {
        private ActorSystem system;
        private IDependencyResolver resolver;

        public Bootstrapper(ActorSystem system, IDependencyResolver resolver)
        {
            this.system = system;
            this.resolver = resolver;
        }

        public void Start()
        {
            CreateTopicService();
        }

        private void CreateTopicService()
        {
            var childProps = system.DI().Props<TopicServiceActor>();
            var supervisorProps = BackoffSupervisor.Props(
                Backoff.OnStop(
                    childProps,
                    "topic-service",
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(15),
                    0.2
                )
            );
            system.ActorOf(supervisorProps, "topic-service-supervisor");
        }
    }
}
