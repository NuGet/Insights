// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Insights.Worker
{
    public class BatchMessageProcessorResult<T>
    {
        public static BatchMessageProcessorResult<T> Empty { get; } = new BatchMessageProcessorResult<T>(
            failed: Array.Empty<T>(),
            tryAgainLater: new Dictionary<TimeSpan, IReadOnlyList<T>>());

        public BatchMessageProcessorResult(IEnumerable<T> failed)
        {
            Failed = failed.ToList();
            TryAgainLater = Empty.TryAgainLater;
        }

        public BatchMessageProcessorResult(IEnumerable<T> failed, IEnumerable<T> tryAgainLater, TimeSpan notBefore)
        {
            Failed = failed.ToList();
            TryAgainLater = new Dictionary<TimeSpan, IReadOnlyList<T>>
            {
                { notBefore, tryAgainLater.ToList() },
            };
        }

        public BatchMessageProcessorResult(IEnumerable<T> failed, IEnumerable<(T Message, TimeSpan NotBefore)> tryAgainLater)
        {
            Failed = failed.ToList();
            TryAgainLater = tryAgainLater
                .ToLookup(x => x.NotBefore)
                .ToDictionary(x => x.Key, x => (IReadOnlyList<T>)x.Select(y => y.Message).ToList());
        }

        public BatchMessageProcessorResult(IEnumerable<T> failed, IReadOnlyDictionary<TimeSpan, IReadOnlyList<T>> tryAgainLater)
        {
            Failed = failed.ToList();
            TryAgainLater = tryAgainLater;
        }

        public IReadOnlyList<T> Failed { get; }
        public IReadOnlyDictionary<TimeSpan, IReadOnlyList<T>> TryAgainLater { get; }
    }

    public class BatchMessageProcessorResult<TResult, TInput> : BatchMessageProcessorResult<TInput>
    {
        public BatchMessageProcessorResult(TResult result, IEnumerable<TInput> failed)
            : base(failed)
        {
            Result = result;
        }

        public BatchMessageProcessorResult(TResult result, IEnumerable<TInput> failed, IEnumerable<TInput> tryAgainLater, TimeSpan notBefore)
            : base(failed, tryAgainLater, notBefore)
        {
            Result = result;
        }

        public BatchMessageProcessorResult(TResult result, IEnumerable<TInput> failed, IEnumerable<(TInput Message, TimeSpan NotBefore)> tryAgainLater)
            : base(failed, tryAgainLater)
        {
            Result = result;
        }

        public BatchMessageProcessorResult(TResult result, IEnumerable<TInput> failed, IReadOnlyDictionary<TimeSpan, IReadOnlyList<TInput>> tryAgainLater)
            : base(failed, tryAgainLater)
        {
            Result = result;
        }

        public TResult Result { get; }
    }
}
