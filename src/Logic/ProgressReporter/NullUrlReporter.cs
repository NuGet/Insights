// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public class NullUrlReporter : IUrlReporter
    {
        public static readonly Lazy<NullUrlReporter> _instance = new Lazy<NullUrlReporter>(() => new NullUrlReporter());

        public static NullUrlReporter Instance => _instance.Value;

        public Task ReportRequestAsync(Guid id, HttpRequestMessage request)
        {
            return Task.CompletedTask;
        }

        public Task ReportResponseAsync(Guid id, HttpResponseMessage response, TimeSpan duration)
        {
            return Task.CompletedTask;
        }
    }
}
