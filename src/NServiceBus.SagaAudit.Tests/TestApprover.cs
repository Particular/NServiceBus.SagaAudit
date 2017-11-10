namespace NServiceBus.SagaAudit.Tests
{
    using System.IO;
    using NUnit.Framework;

    static class TestApprover
    {
#if NET452
        public static void Verify(string text)
        {
            var writer = new ApprovalTests.ApprovalTextWriter(text);
            var namer = new ApprovalNamer();
            ApprovalTests.Approvals.Verify(writer, namer, ApprovalTests.Approvals.GetReporter());
        }

        class ApprovalNamer : ApprovalTests.Namers.UnitTestFrameworkNamer
        {
            public override string SourcePath { get; } = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "ApprovalFiles");
        }
#else
        public static void Verify(string text)
        {
            Assert.Inconclusive("ApprovalTests only work in full .NET");
        }
#endif
    }
}
