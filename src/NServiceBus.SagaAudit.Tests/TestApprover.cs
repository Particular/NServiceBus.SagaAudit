namespace NServiceBus.SagaAudit.Tests
{
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
            public ApprovalNamer()
            {
                var assemblyPath = GetType().Assembly.Location;
                var assemblyDir = System.IO.Path.GetDirectoryName(assemblyPath);

                sourcePath = System.IO.Path.Combine(assemblyDir, "..", "..", "..", "ApprovalFiles");
            }

            public override string SourcePath => sourcePath;

            readonly string sourcePath;
        }
#else
        public static void Verify(string text)
        {
            NUnit.Framework.Assert.Inconclusive("ApprovalTests only work in full .NET");
        }
#endif
    }
}
