// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class HashOutput
    {
        public byte[] MD5 { get; internal set; }
        public byte[] SHA1 { get; internal set; }
        public byte[] SHA256 { get; internal set; }
        public byte[] SHA512 { get; internal set; }
    }
}
