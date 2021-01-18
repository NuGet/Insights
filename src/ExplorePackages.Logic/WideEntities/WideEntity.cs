using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Knapcode.ExplorePackages.WideEntities
{
    public class WideEntity
    {
        private readonly IReadOnlyList<ReadOnlyMemory<byte>> _chunks;

        internal WideEntity(ICollection<WideEntitySegment> segments)
        {
            PartitionKey = segments.Select(x => x.PartitionKey).Distinct().Single();
            RowKey = segments.Select(x => x.RowKeyPrefix).Distinct().Single();

            var orderedSegments = segments.OrderBy(x => x.Index).ToList();
            var firstSegment = orderedSegments.First();
            Timestamp = firstSegment.Timestamp;
            ETag = firstSegment.ETag;

            if (firstSegment.Index != 0)
            {
                throw new ArgumentException("The first segment should have an index of 0.", nameof(segments));
            }

            if (segments.Count != firstSegment.SegmentCount)
            {
                throw new ArgumentException("The number of segments provided must match the segment count property on the first segment.");
            }

            SegmentCount = segments.Count;
            _chunks = orderedSegments.SelectMany(x => x.Chunks).ToList();
        }

        public string PartitionKey { get; }
        public string RowKey { get; }
        public DateTimeOffset Timestamp { get; }
        public string ETag { get; }
        public int SegmentCount { get; }
        public Stream GetStream() => new ChunkStream(_chunks);
    }
}
