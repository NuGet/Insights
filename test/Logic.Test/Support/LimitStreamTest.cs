// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class LimitStreamTest
    {
        public static IEnumerable<object[]> CanReadFullStreamData
        {
            get
            {
                var interestingSizes = new[] { 0, 1, 2, 3, 4, 5, 497, 498, 499, 500, 501, 502, 503, 998, 999, 1000, 1001, 1002 };
                foreach (var largerBy in interestingSizes)
                {
                    foreach (var bufferSize in interestingSizes.Where(b => b > 0))
                    {
                        yield return new object[] { largerBy, bufferSize };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(CanReadFullStreamData))]
        public void CanReadFullStream(int largerBy, int bufferSize)
        {
            var data = GetRandomBytes(length: 500);
            var inputStream = new MemoryStream(data);
            var limitStream = new LimitStream(inputStream, data.Length + largerBy);
            var buffer = new byte[bufferSize];
            var outputStream = new MemoryStream();

            int read;
            while ((read = limitStream.Read(buffer, 0, bufferSize)) > 0)
            {
                outputStream.Write(buffer, 0, read);
            }

            Assert.False(limitStream.Truncated);
            Assert.Equal(500, limitStream.ReadBytes);
            Assert.Equal(data, outputStream.ToArray());
        }

        public static IEnumerable<object[]> CanReadPartOfStreamData
        {
            get
            {
                foreach (var smallerBy in new[] { 1, 2, 3, 4, 5, 497, 498, 499 })
                {
                    foreach (var bufferSize in new[] { 1, 2, 3, 4, 5, 497, 498, 499, 500, 501, 502, 503, 998, 999, 1000, 1001, 1002 })
                    {
                        yield return new object[] { smallerBy, bufferSize };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(CanReadPartOfStreamData))]
        public void CanReadPartOfStream(int smallerBy, int bufferSize)
        {
            var data = GetRandomBytes(length: 500);
            var inputStream = new MemoryStream(data);
            var limit = data.Length - smallerBy;
            var limitStream = new LimitStream(inputStream, limit);
            var buffer = new byte[bufferSize];
            var outputStream = new MemoryStream();

            int read;
            while ((read = limitStream.Read(buffer, 0, bufferSize)) > 0)
            {
                outputStream.Write(buffer, 0, read);
            }

            Assert.True(limitStream.Truncated);
            Assert.Equal(limit, limitStream.ReadBytes);
            Assert.Equal(data.Take(limit).ToArray(), outputStream.ToArray());
        }

        [Fact]
        public void RestoresOverflowByteInBuffer()
        {
            var data = new byte[500];
            Array.Fill<byte>(data, 1);
            var buffer = new byte[1000];
            Array.Fill<byte>(buffer, 2);
            var inputStream = new MemoryStream(data);
            var limitStream = new LimitStream(inputStream, 250);
            var outputStream = new MemoryStream();

            int read;
            while ((read = limitStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                outputStream.Write(buffer, 0, read);
            }

            Assert.True(limitStream.Truncated);
            Assert.Equal(250, limitStream.ReadBytes);
            Assert.Equal(data.Take(limitStream.LimitBytes).ToArray(), outputStream.ToArray());
            Assert.All(buffer.Skip(limitStream.LimitBytes), b => Assert.Equal(2, b));
        }

        private static byte[] GetRandomBytes(int length = 500, int seed = 1)
        {
            var data = new byte[length];
            var random = new Random(seed);
            random.NextBytes(data);
            return data;
        }
    }
}
