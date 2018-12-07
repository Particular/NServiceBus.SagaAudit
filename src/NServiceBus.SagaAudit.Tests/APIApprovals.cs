using System.Runtime.CompilerServices;
using NServiceBus.SagaAudit;
using NUnit.Framework;
using Particular.Approvals;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Approve()
    {
        var publicApi = ApiGenerator.GeneratePublicApi(typeof(SagaAuditHeaders).Assembly);
        Approver.Verify(publicApi);
    }
}