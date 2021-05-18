// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGet.Insights
{
    public interface IUrlReporter
    {
        Task ReportRequestAsync(Guid id, HttpRequestMessage request);
        Task ReportResponseAsync(Guid id, HttpResponseMessage response, TimeSpan duration);
    }
}
