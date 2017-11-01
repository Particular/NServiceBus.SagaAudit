namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using System.Collections.Concurrent;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using NServiceBus;

    class Program
    {
        static void Main()
        {
            Console.Title = "NServiceBus.SagaAudit.SmokeTest";

            var builder = new ContainerBuilder();

            var masters = new ConcurrentDictionary<Guid, bool>();
            var cancellationSource = new CancellationTokenSource();

            builder.RegisterInstance(masters);
            builder.RegisterInstance(cancellationSource);
            var container = builder.Build();

            var busConfiguration = new BusConfiguration();
            busConfiguration.EndpointName("NServiceBus.SagaAudit.SmokeTest");
            busConfiguration.UseContainer<AutofacBuilder>(c => c.ExistingLifetimeScope(container));
            busConfiguration.UseSerialization<JsonSerializer>();
            busConfiguration.EnableInstallers();
            busConfiguration.UsePersistence<InMemoryPersistence>();
            busConfiguration.AuditSagaStateChanges("particular.servicecontrol");

            busConfiguration.UseTransport<MsmqTransport>();

            var startableBus = Bus.Create(busConfiguration);
            var bus = startableBus.Start();

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
                    bus.SendLocal(startMasterAlpha);
                    bus.SendLocal(startMasterBeta);
                }
                do
                {
                    try
                    {
                        Task.Delay(2000, token).GetAwaiter().GetResult();
                    }
                    catch (TaskCanceledException)
                    {
                    }
                } while (!token.IsCancellationRequested);
            }
            finally
            {
                bus.Dispose();
                Console.ReadKey();
            }
        }
    }
}