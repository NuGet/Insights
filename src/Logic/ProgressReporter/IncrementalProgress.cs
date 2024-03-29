﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class IncrementalProgress
    {
        private readonly IProgressReporter _progressReporter;
        private int _current;
        private readonly decimal _total;

        public IncrementalProgress(IProgressReporter progressReporter, int total)
        {
            _progressReporter = progressReporter;
            _current = 0;
            _total = total;
        }

        public Task ReportProgressAsync(string message)
        {
            if (_current < _total)
            {
                _current++;
            }

            return _progressReporter.ReportProgressAsync(_current / _total, message);
        }
    }
}
