using System.Runtime.CompilerServices;
using NServiceBus.Features;
using NServiceBus.SagaAudit.Tests;
using NUnit.Framework;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void Approve()
    {
        var publicApi = ApiGenerator.GeneratePublicApi(typeof(SagaAuditFeature).Assembly, excludeAttributes: new[] { "System.Runtime.Versioning.TargetFrameworkAttribute" });
        TestApprover.Verify(publicApi);
    }
}