// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using NuGet.Insights.Worker.AuxiliaryFileUpdater;
using NuGet.Insights.Worker.KustoIngestion;
using NuGet.Insights.Worker.ReferenceTracking;
using NuGet.Insights.Worker.TimedReprocess;
using NuGet.Insights.Worker.Workflow;

namespace NuGet.Insights.Worker
{
    public class TimerComparer : IComparer<ITimer>
    {
        public static TimerComparer Instance { get; } = new TimerComparer();

        private static readonly ConcurrentDictionary<Type, int> TypeCache = new();

        private static readonly IReadOnlyList<Func<Type, bool>> DesiredGrouping = new Func<Type, bool>[]
        {
            x => x.IsAssignableTo(typeof(WorkflowTimer)),

            x => x.IsAssignableTo(typeof(TimedReprocessTimer)),

            x => x.IsAssignableTo(typeof(CatalogScanUpdateTimer)),

            x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(CleanupOrphanRecordsTimer<>),

            x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(AuxiliaryFileUpdaterTimer<>),

            x => x.IsAssignableTo(typeof(KustoIngestionTimer)),
        };

        public int Compare(ITimer x, ITimer y)
        {
            return GetIndex(x).CompareTo(GetIndex(y));
        }

        private int GetIndex(ITimer x)
        {
            return TypeCache.GetOrAdd(x.GetType(), type =>
            {
                var i = 0;
                foreach (var predicate in DesiredGrouping)
                {
                    if (predicate(type))
                    {
                        return i;
                    }

                    i++;
                }

                return i;
            });
        }
    }
}
