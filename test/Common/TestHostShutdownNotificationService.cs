﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;

namespace Microsoft.Azure.WebJobs.Extensions.DurableTask.Tests
{
    internal class TestHostShutdownNotificationService : IHostLifetime
    {
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public CancellationToken OnStopped => this.cts.Token;

        public CancellationToken OnStarted => throw new System.NotImplementedException();

        public CancellationToken OnStopping => throw new System.NotImplementedException();

        public void SignalShutdown() => this.cts.Cancel();
    }
}