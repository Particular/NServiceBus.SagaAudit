namespace NServiceBus.SagaAudit.SmokeTest
{
    using System;
    using System.Collections.Concurrent;
    using System.Linq;
    using System.Threading;
    using Logging;
    using NServiceBus;

    class MasterFinishedHandler : IHandleMessages<MasterFinished>
    {
        readonly ConcurrentDictionary<Guid,bool> masters;
        readonly CancellationTokenSource tokenSource;
        static ILog Log = LogManager.GetLogger<MasterFinishedHandler>();

        public MasterFinishedHandler(ConcurrentDictionary<Guid,bool> masters, CancellationTokenSource tokenSource)
        {
            this.masters = masters;
            this.tokenSource = tokenSource;
        }

        public void Handle(MasterFinished message)
        {
            Log.Info($"Master {message.Identifier} finished, checking if program can exit.");

            masters[message.Identifier] = true;

            if (masters.Values.All(finished => finished))
            {
                Log.Info("Program cancelling");
                tokenSource.Cancel();
            }
        }
    }
}
