using System;
using NServiceBus;
using NServiceBus.Features;

public class Program
{
    public static void Main()
    {
        var busConfiguration = new BusConfiguration();

        busConfiguration.DisableFeature<SecondLevelRetries>();
        busConfiguration.UseSerialization<JsonSerializer>();
        busConfiguration.UsePersistence<InMemoryPersistence>();
        busConfiguration.AuditSagaStateChanges(Address.Parse("Particular.ServiceControl2222"));

        using (var bus = Bus.Create(busConfiguration).Start())
        {
            Console.WriteLine("Press 'enter' to send a StartOrder messages");
            Console.WriteLine("Press any other key to exit");
            while (true)
            {
                var key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key != ConsoleKey.Enter)
                {
                    return;
                }
                var message = new Message1
                {
                    SomeId = Guid.NewGuid()
                };
                bus.SendLocal(message);
            }
        }
    }
}
