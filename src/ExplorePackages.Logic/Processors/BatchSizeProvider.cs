using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class BatchSizeProvider : IBatchSizeProvider
    {
        private const int MinimumForCatalog = 21;
        private static readonly IReadOnlyDictionary<BatchSizeType, Bounds> DefaultBatchSizes = new Dictionary<BatchSizeType, Bounds>
        {
            {
                BatchSizeType.DependenciesToDatabase,
                new Bounds(MinimumForCatalog, 1000)
            },
            {
                BatchSizeType.DependencyPackagesToDatabase_PackageRegistrations,
                new Bounds(MinimumForCatalog, 100)
            },
            {
                BatchSizeType.DependencyPackagesToDatabase_Packages,
                new Bounds(1, 10000)
            },
            {
                BatchSizeType.MZip,
                new Bounds(MinimumForCatalog, 1000)
            },
            {
                BatchSizeType.MZipToDatabase,
                new Bounds(MinimumForCatalog, 100)
            },
            {
                BatchSizeType.PackageDownloadsToDatabase,
                new Bounds(1, 1000)
            },
            {
                BatchSizeType.PackageQueries,
                new Bounds(MinimumForCatalog, 5000)
            },
            {
                BatchSizeType.PackageQueryService_MatchedPackages,
                new Bounds(1, 1000)
            },
            {
                BatchSizeType.ReprocessCrossCheckDiscrepancies,
                new Bounds(MinimumForCatalog, 5000)
            },
            {
                BatchSizeType.V2ToDatabase,
                new Bounds(10, 100) // 10 for a minimum is just a guess. This number has to be higher than the maximum
                                    // number of V2 packages that have the same LastEdited or Created timestamp.
            },
        };

        private readonly ILogger<BatchSizeProvider> _logger;
        private decimal _currentPercent = 1;

        public BatchSizeProvider(ILogger<BatchSizeProvider> logger)
        {
            _logger = logger;
        }

        public void Decrease()
        {
            SetPercent(_currentPercent / 2);
        }

        public void Increase()
        {
            SetPercent(_currentPercent * 2);
        }

        public void Reset()
        {
            SetPercent(1);
        }

        private void SetPercent(decimal newPercent)
        {
            if (newPercent > 1)
            {
                newPercent = 1;
            }

            if (newPercent == _currentPercent)
            {
                return;
            }

            _logger.LogInformation($"Batch sizes will now be {newPercent * 100}% of their configured value or their configured minimum, whichever is higher.");
            _currentPercent = newPercent;
        }

        public int Get(BatchSizeType type)
        {
            if (!DefaultBatchSizes.TryGetValue(type, out var bounds))
            {
                throw new NotSupportedException($"The batch size type '{type}' is not supported.");
            }

            var current = (int)Math.Round(bounds.Initial * _currentPercent);
            if (current < bounds.Minimum)
            {
                return bounds.Minimum;
            }

            return current;
        }

        private class Bounds
        {
            public Bounds(int minimum, int initial)
            {
                Minimum = minimum;
                Initial = initial;
            }

            public int Minimum { get; }
            public int Initial { get; }
        }
    }
}
