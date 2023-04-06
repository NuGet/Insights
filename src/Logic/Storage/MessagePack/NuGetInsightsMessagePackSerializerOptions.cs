// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Ben.Collections.Specialized;
using MessagePack;

#nullable enable

namespace NuGet.Insights
{
    public class NuGetInsightsMessagePackSerializerOptions : MessagePackSerializerOptions
    {
        protected internal NuGetInsightsMessagePackSerializerOptions(MessagePackSerializerOptions copyFrom) : base(copyFrom)
        {
        }

        public InternPool? InternPool { get; internal init; }
    }
}
