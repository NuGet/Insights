// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Security.Cryptography;

namespace NuGet.Insights
{
    public static class IncrementalHash
    {
        public static IIncrementalHash CreateSHA256()
        {
            return new IncrementalHashes(GetSHA256());
        }

        public static IIncrementalHash CreateNone()
        {
            return new IncrementalHashes();
        }

        public static IIncrementalHash CreateAll()
        {
            return new IncrementalHashes(GetMD5(), GetSHA1(), GetSHA256(), GetSHA512());
        }

        private static CryptoTransformAdapter GetMD5()
        {
            return new CryptoTransformAdapter { HashAlgorithm = MD5.Create(), SetOutput = (x, h) => x.MD5 = h };
        }

        private static CryptoTransformAdapter GetSHA1()
        {
            return new CryptoTransformAdapter { HashAlgorithm = SHA1.Create(), SetOutput = (x, h) => x.SHA1 = h };
        }

        private static CryptoTransformAdapter GetSHA256()
        {
            return new CryptoTransformAdapter { HashAlgorithm = SHA256.Create(), SetOutput = (x, h) => x.SHA256 = h };
        }

        private static CryptoTransformAdapter GetSHA512()
        {
            return new CryptoTransformAdapter { HashAlgorithm = SHA512.Create(), SetOutput = (x, h) => x.SHA512 = h };
        }

        private record CryptoTransformAdapter
        {
            public HashAlgorithm HashAlgorithm { get; init; }
            public Action<HashOutput, byte[]> SetOutput { get; init; }
        }

        private class IncrementalHashes : IIncrementalHash
        {
            private readonly CryptoTransformAdapter[] _adapters;

            public IncrementalHashes(params CryptoTransformAdapter[] adapters)
            {
                _adapters = adapters;
            }

            public HashOutput Output { get; } = new HashOutput();

            public void Dispose()
            {
                foreach (var adapter in _adapters)
                {
                    adapter.HashAlgorithm.Dispose();
                }
            }

            public void TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount)
            {
                foreach (var adapter in _adapters)
                {
                    adapter.HashAlgorithm.TransformBlock(inputBuffer, inputOffset, inputCount, inputBuffer, 0);
                }
            }

            public void TransformFinalBlock()
            {
                foreach (var adapter in _adapters)
                {
                    adapter.HashAlgorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    adapter.SetOutput(Output, adapter.HashAlgorithm.Hash);
                }
            }
        }
    }
}
