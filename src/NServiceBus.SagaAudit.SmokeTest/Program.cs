namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using NServiceBus;

    class Program
    {
        static void Main()
        {
            AsyncMain().GetAwaiter().GetResult();
        }

        static async Task AsyncMain()
        {
            Console.Title = "NServiceBus.SagaAudit.SmokeTest";

            var masters = new ConcurrentDictionary<Guid, bool>();
            var cancellationSource = new CancellationTokenSource();



            var busConfiguration = new EndpointConfiguration("NServiceBus.SagaAudit.SmokeTest");
            busConfiguration.RegisterComponents(c =>
            {
                c.RegisterSingleton(masters);
                c.RegisterSingleton(cancellationSource);
            });
            busConfiguration.UseSerialization<JsonSerializer>();
            busConfiguration.EnableInstallers();
            busConfiguration.UsePersistence<InMemoryPersistence>();
            busConfiguration.SendFailedMessagesTo("error");
            busConfiguration.AuditProcessedMessagesTo("audit");
            busConfiguration.AuditSagaStateChanges("particular.servicecontrol");

            var routing = busConfiguration.UseTransport<MsmqTransport>().Routing();
            routing.RouteToEndpoint(typeof(Program).Assembly, "NServiceBus.SagaAudit.SmokeTest");
            routing.RegisterPublisher(typeof(Program).Assembly, "NServiceBus.SagaAudit.SmokeTest");

            var endpoint = await Endpoint.Start(busConfiguration);

            var token = cancellationSource.Token;

            try
            {
                for (var i = 1; i <= 10; i++)
                {
                    var masterId = Guid.NewGuid();
                    masters.TryAdd(masterId, false);
                    Console.WriteLine($"Sending StartMaster for {masterId}");
                    var startMasterAlpha = new StartMasterAlpha
                    {
                        Identifier = masterId,
                        WorkRequired = i
                    };
                    var startMasterBeta = new StartMasterBeta
                    {
                        Identifier = masterId
                    };
                    await Task.WhenAll(endpoint.SendLocal(startMasterAlpha), endpoint.SendLocal(startMasterBeta));
                }
                do
                {
                    try
                    {
                        await Task.Delay(2000, token);
                    }
                    catch (TaskCanceledException)
                    {
                    }
                } while (!token.IsCancellationRequested);
            }
            finally
            {
                await endpoint.Stop();
                Console.WriteLine("Smoke test completed. Press any key to exit.");
                Console.ReadKey();
            }
        }
    }
}