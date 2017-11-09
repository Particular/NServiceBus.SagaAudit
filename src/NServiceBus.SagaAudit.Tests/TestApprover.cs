namespace NServiceBus.SagaAudit.Tests
{
    using System.IO;
    using ApprovalTests;
    using ApprovalTests.Namers;
    using NUnit.Framework;

    static class TestApprover
    {
        public static void Verify(string text)
        {
            var writer = new ApprovalTextWriter(text);
            var namer = new ApprovalNamer();
            Approvals.Verify(writer, namer, Approvals.GetReporter());
        }

        class ApprovalNamer : UnitTestFrameworkNamer
        {
            public ApprovalNamer()
            {
                var assemblyPath = TestContext.CurrentContext.TestDirectory;
                var assemblyDir = Path.GetDirectoryName(assemblyPath);

                sourcePath = Path.Combine(assemblyDir, "..", "..", "ApprovalFiles");
            }

            public override string SourcePath => sourcePath;

            readonly string sourcePath;
        }
    }
}