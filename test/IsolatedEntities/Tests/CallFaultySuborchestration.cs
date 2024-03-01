﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DurableTask.Core.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.DurableTask;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Entities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsolatedEntities;

class CallFaultySuborchestration : Test
{
    // this is not an entity test... but it's a good place to put this test

    private readonly bool nested;

    public CallFaultySuborchestration(bool nested)
    {
        this.nested = nested;
    }

    public override string Name => $"{base.Name}.{(this.nested ? "Nested" : "NotNested")}";

    public override async Task RunAsync(TestContext context)
    {
        string orchestrationName = nameof(CallFaultySuborchestrationOrchestration);
        string instanceId = await context.Client.ScheduleNewOrchestrationInstanceAsync(orchestrationName, this.nested);
        var metadata = await context.Client.WaitForInstanceCompletionAsync(instanceId, true);

        Assert.Equal(OrchestrationRuntimeStatus.Completed, metadata.RuntimeStatus);
        Assert.Equal("ok", metadata.ReadOutputAs<string>());
    }
}

class CallFaultySuborchestrationOrchestration
{
    readonly ILogger logger;

    public CallFaultySuborchestrationOrchestration(ILogger<CallFaultyEntityOrchestration> logger)
    {
        this.logger = logger;
    }

    [Function(nameof(FaultySuborchestration))]
    public void FaultySuborchestration([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        bool nested = context.GetInput<bool>();

        if (!nested)
        {
            this.MethodThatThrowsException();
        }
        else
        {
            this.MethodThatThrowsNestedException();
        }
    }

    void MethodThatThrowsNestedException()
    {
        try
        {
            this.MethodThatThrowsException();
        }
        catch (Exception e)
        {
            throw new Exception("KABOOOOOM", e);
        }
    }

    void MethodThatThrowsException()
    {
        throw new Exception("KABOOM");
    }

    [Function(nameof(CallFaultySuborchestrationOrchestration))]
    public async Task<string> RunAsync([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        bool nested = context.GetInput<bool>();

        try
        {
            await context.CallSubOrchestratorAsync(nameof(FaultySuborchestration), nested);
            throw new Exception("expected suborchestrator to throw exception, but none was thrown");
        }
        catch (TaskFailedException taskFailedException)
        {
            Assert.NotNull(taskFailedException.FailureDetails);

            if (!nested)
            {
                Assert.Equal("KABOOM", taskFailedException.FailureDetails.ErrorMessage);
                Assert.Contains(nameof(MethodThatThrowsException), taskFailedException.FailureDetails.StackTrace);
            }
            else
            {
                Assert.Equal("KABOOOOOM", taskFailedException.FailureDetails.ErrorMessage);
                Assert.Contains(nameof(MethodThatThrowsNestedException), taskFailedException.FailureDetails.StackTrace);

                Assert.NotNull(taskFailedException.FailureDetails.InnerFailure);
                Assert.Equal("KABOOM", taskFailedException.FailureDetails.InnerFailure!.ErrorMessage);
                Assert.Contains(nameof(MethodThatThrowsException), taskFailedException.FailureDetails.InnerFailure.StackTrace);
            }
        }
        catch (Exception e)
        {
            throw new Exception($"wrong exception thrown", e);
        }

        return "ok";
    }
}