// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Azure.Core;
using Azure.Core.Pipeline;

#nullable enable

namespace NuGet.Insights.MemoryStorage
{
    public static class ClientOptionsExtensions
    {
        public static T AddBrokenTransport<T>(this T options) where T : ClientOptions
        {
            if (options.Transport != BrokenTransport.Instance)
            {
                options.Transport = BrokenTransport.Instance;
            }

            return options;
        }

        private class BrokenTransport : HttpPipelineTransport
        {
            public static BrokenTransport Instance { get; } = new();

            private static string GetRejectionMessage()
            {
                StackTrace stackTrace = new StackTrace();
                StackFrame[] stackFrames = stackTrace.GetFrames();

                MethodBase? overrideCandidate = null;

                foreach (var stackFrame in stackFrames)
                {
                    if (stackFrame is null)
                    {
                        continue;
                    }

                    MethodBase? methodBase = stackFrame.GetMethod();
                    if (methodBase is null)
                    {
                        continue;
                    }

                    if (overrideCandidate is null
                        && methodBase.IsPublic
                        && methodBase.IsVirtual
                        && methodBase.DeclaringType?.FullName?.StartsWith("Azure.", StringComparison.Ordinal) == true)
                    {
                        overrideCandidate = methodBase;
                    }
                }

                var message = "Sending HTTP request is not supported for the in-memory storage. You must override a service client method to make this work.";

                if (overrideCandidate is not null)
                {
                    message += $"{Environment.NewLine}Try overriding this method in one of the in-memory service clients:{Environment.NewLine}{overrideCandidate}";
                }

                return message;
            }

            public override Request CreateRequest()
            {
                throw new NotSupportedException(GetRejectionMessage());
            }

            public override void Process(HttpMessage message)
            {
                throw new NotSupportedException(GetRejectionMessage());
            }

            public override ValueTask ProcessAsync(HttpMessage message)
            {
                throw new NotSupportedException(GetRejectionMessage());
            }
        }
    }
}
