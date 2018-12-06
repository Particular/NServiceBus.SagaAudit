using NServiceBus.Features;
using NUnit.Framework;
using Particular.Approvals;
using PublicApiGenerator;

[TestFixture]
public class APIApprovals
{
    [Test]
    public void Approve()
    {
        var publicApi = ApiGenerator.GeneratePublicApi(typeof(SagaAuditFeature).Assembly, excludeAttributes: new[]
        {
                "Particular.Licensing.ReleaseDateAttribute",
                "System.Runtime.Versioning.TargetFrameworkAttribute"
            });

#if NETFRAMEWORK
        Approver.Verify(publicApi, scenario: "netframework");
#endif
#if NETCOREAPP
            Approver.Verify(publicApi, scenario: "netstandard");
#endif
    }
}
