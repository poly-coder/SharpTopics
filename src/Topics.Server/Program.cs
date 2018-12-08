using Akka.Configuration;
using Akka.Actor;
using Akka.DI.AutoFac;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using Autofac;
using Akka.DI.Core;
using System.Reflection;
using Topics.Protocols;

namespace Topics.Server
{
    public class ActorSystemOptions
    {
        public string Name { get; set; }
        public string ConfigFile { get; set; }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var config = CreateConfiguration(args);

            var system = CreateActorSystem(config.GetSection("ActorSystem").Get<ActorSystemOptions>());

            var resolver = CreateServiceResolver(system, config);

            new Bootstrapper(system, resolver).Start();

            ConsoleCancelEventHandler cancelHandler = null;
            cancelHandler = (sender, e) =>
            {
                e.Cancel = true;
                Console.CancelKeyPress -= cancelHandler;
                system.Terminate();
            };
            Console.CancelKeyPress += cancelHandler;

            system.WhenTerminated.GetAwaiter().GetResult();
        }

        private static IDependencyResolver CreateServiceResolver(ActorSystem system, IConfigurationRoot config)
        {
            var builder = new ContainerBuilder();

            builder.RegisterInstance(config)
                .As<IConfigurationRoot>()
                .As<IConfiguration>();

            builder
                .Register(cc => 
                    cc.Resolve<IConfigurationRoot>()
                        .GetSection("TopicService:GrpcHost")
                        .Get<TopicServiceOptions>())
                .InstancePerDependency()
                .As<TopicServiceOptions>();

            // register actors in this assembly
            builder.RegisterAssemblyTypes(Assembly.GetEntryAssembly())
                .Where(t => !t.IsAbstract && t.IsAssignableTo<ActorBase>())
                .AsSelf();

            var container = builder.Build();
            var resolver = new AutoFacDependencyResolver(container, system);
            return resolver;
        }

        private static ActorSystem CreateActorSystem(ActorSystemOptions opts)
        {
            var hocon = File.ReadAllText(opts.ConfigFile);
            var system = ActorSystem.Create(opts.Name, ConfigurationFactory.ParseString(hocon));
            return system;
        }

        private static IConfigurationRoot CreateConfiguration(string[] args)
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddEnvironmentVariables("TOPICSERVER-")
                .AddCommandLine(args)
                .Build();
        }
    }
}
