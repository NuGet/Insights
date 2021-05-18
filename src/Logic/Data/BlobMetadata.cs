// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class BlobMetadata
    {
        public BlobMetadata(bool exists, bool hasContentMD5Header, string contentMD5)
        {
            Exists = exists;
            HasContentMD5Header = hasContentMD5Header;
            ContentMD5 = contentMD5;
        }

        public bool Exists { get; }
        public bool HasContentMD5Header { get; }
        public string ContentMD5 { get; }
    }
}
