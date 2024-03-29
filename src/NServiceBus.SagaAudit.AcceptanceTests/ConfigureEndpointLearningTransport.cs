﻿using System;
using System.IO;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Transport;
using NUnit.Framework;

public class ConfigureEndpointLearningTransport
{
    public Task Cleanup()
    {
        if (Directory.Exists(storageDir))
        {
            Directory.Delete(storageDir, true);
        }

        return Task.CompletedTask;
    }

    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings settings, PublisherMetadata publisherMetadata)
    {
        var testRunId = TestContext.CurrentContext.Test.ID;

        string tempDir;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            //can't use bin dir since that will be too long on the build agents
            tempDir = @"c:\temp";
        }
        else
        {
            tempDir = Path.GetTempPath();
        }

        storageDir = Path.Combine(tempDir, testRunId);

        //we want the tests to be exposed to concurrency
        configuration.LimitMessageProcessingConcurrencyTo(PushRuntimeSettings.Default.MaxConcurrency);

        configuration.UseTransport(new LearningTransport { StorageDirectory = storageDir });

        return Task.CompletedTask;
    }

    string storageDir;
}