using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Knapcode.ExplorePackages.Logic
{
    public class BatchSizeProvider : IBatchSizeProvider
    {
        /// <summary>
        /// The minimum is 2 because a batch size less than this would not allow reliable commit timestamp traversal.
        /// A cursor in the catalog is a timestamp representing up to what time in the catalog has been processed. If
        /// the batch size was 1 then we would be unable to determine if the item in the catalog represents the whole
        /// catalog commit (batch of items with the same commit timestamp).
        /// </summary>
        private const int MinimumForCatalog = 2;

        private static readonly IReadOnlyDictionary<BatchSizeType, Bounds> DefaultBatchSizes = new Dictionary<BatchSizeType, Bounds>
        {
            {
                BatchSizeType.DependenciesToDatabase,
                new Bounds(MinimumForCatalog, 1000)
            },
            {
                BatchSizeType.DependencyPackagesToDatabase_PackageRegistrations,
                new Bounds(MinimumForCatalog, 10)
            },
            {
                BatchSizeType.DependencyPackagesToDatabase_Packages,
                new Bounds(1, 1000)
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
                new Bounds(50, 1000) // 50 is not a strict minimum but it's too slow if the batch size is much lower.
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
        private int? _override;

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

            var initial = _override ?? bounds.Initial;

            var current = (int)Math.Round(initial * _currentPercent);
            if (current < bounds.Minimum)
            {
                return bounds.Minimum;
            }

            return current;
        }

        public void Set(int batchSize)
        {
            _override = batchSize;
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
