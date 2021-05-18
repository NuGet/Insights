// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;

namespace NuGet.Insights
{
    public class UrlReporterProvider
    {
        private readonly AsyncLocal<IUrlReporter> _currentUrlReporter;

        public UrlReporterProvider()
        {
            _currentUrlReporter = new AsyncLocal<IUrlReporter>();
        }

        public void SetUrlReporter(IUrlReporter urlReporter)
        {
            _currentUrlReporter.Value = urlReporter;
        }

        public IUrlReporter GetUrlReporter()
        {
            return _currentUrlReporter.Value ?? NullUrlReporter.Instance;
        }
    }
}
