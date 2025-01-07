namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using NServiceBus;

    class Program
    {
        static async Task Main()
        {
            Console.Title = "NServiceBus.SagaAudit.SmokeTest";

            var masters = new ConcurrentDictionary<Guid, bool>();
            var cancellationSource = new CancellationTokenSource();

            var busConfiguration = new EndpointConfiguration("NServiceBus.SagaAudit.SmokeTest");
            busConfiguration.RegisterComponents(c =>
            {
                c.AddSingleton(masters);
                c.AddSingleton(cancellationSource);
            });
            busConfiguration.UseSerialization<SystemJsonSerializer>();
            busConfiguration.EnableInstallers();
            busConfiguration.UsePersistence<LearningPersistence>();
            busConfiguration.SendFailedMessagesTo("error");
            busConfiguration.AuditProcessedMessagesTo("audit");
            busConfiguration.AuditSagaStateChanges("particular.servicecontrol");

            var routing = busConfiguration.UseTransport(new LearningTransport());
            routing.RouteToEndpoint(typeof(Program).Assembly, "NServiceBus.SagaAudit.SmokeTest");

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
                }
                while (!token.IsCancellationRequested);
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