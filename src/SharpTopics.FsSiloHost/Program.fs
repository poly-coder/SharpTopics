open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Orleans
open Orleans.Runtime.Configuration
open Orleans.Hosting
open System
open FSharp.Control.Tasks.V2
open SharpTopics.FsInterfaces
open SharpTopics.FsGrains
open SharpTopics.Core
open SharpTopics.AzureStorage
open Microsoft.Extensions.Configuration
open Microsoft.WindowsAzure.Storage
open Orleans.ApplicationParts

type AzureMessageStoreConfig() =
    member val ConnectionString = "" with get, set
    member val TableName = "" with get, set

let createConfig argv =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional = true, reloadOnChange = false)
        .AddEnvironmentVariables("TOPIC_SERVER_")
        .AddCommandLine(argv: string[])
        .Build()

let configureParts configuration (parts: IApplicationPartManager) =
    do parts
        .AddApplicationPart(typeof<IMessagePublisher>.Assembly)
        .AddApplicationPart(typeof<MessagePublisherGrain>.Assembly)
        .WithCodeGeneration()
    |> ignore

let configureLogging configuration (logging: ILoggingBuilder) =
    do logging
        .AddConsole() 
    |> ignore

let configureServices configuration (services: IServiceCollection) =
    do services

        .AddSingleton<IConfigurationRoot>(configuration: IConfigurationRoot)

        .AddSingleton<AzureMessageStoreConfig>(fun svc ->
            svc
                .GetService<IConfigurationRoot>()
                .GetSection("MessageStorage:AzureStorage")
                .Get<AzureMessageStoreConfig>()
            |> fun cnf -> if obj.ReferenceEquals(cnf, null) 
                          then AzureMessageStoreConfig()
                          else cnf
        )

        .AddSingleton<IMessageStore, AzureTableMessageStore>(fun svc ->
            let cnf = svc.GetService<AzureMessageStoreConfig>()
            let account = Storage.parseAccount cnf.ConnectionString
            let client = account.CreateCloudTableClient()
            let table = client.GetTableReference(cnf.TableName)
            AzureTableMessageStore(table, AzureTableMessageStoreOptions.standard)
        )

    |> ignore

let buildSiloHost configuration =
    let builder = new SiloHostBuilder()
    builder
        .UseLocalhostClustering()
        .ConfigureApplicationParts(Action<_> (configureParts configuration))
        .ConfigureLogging(configureLogging configuration)
        .ConfigureServices(configureServices configuration)
        .Build()

[<EntryPoint>]
let main argv =
    let t = task {
        let configuration = createConfig argv
        let host = buildSiloHost configuration
        do! host.StartAsync()

        printfn "Press any keys to terminate..."
        Console.Read() |> ignore

        do! host.StopAsync()

        printfn "Silohost is stopped"
    }
    
    t.Wait()

    0 // return an integer exit code
