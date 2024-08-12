// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class TempStreamService
    {
        private readonly IServiceProvider _serviceProvider;

        public TempStreamService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<TempStreamResult> CopyToTempStreamAsync(Func<Stream> getStream, Func<string> getTempFileName, long length, Func<IIncrementalHash> getHashAlgorithm)
        {
            var writer = GetWriter();
            TempStreamResult result;
            do
            {
                using var src = getStream();
                using var hashAlgorithm = getHashAlgorithm();
                result = await writer.CopyToTempStreamAsync(src, getTempFileName, length, hashAlgorithm);
            }
            while (result.Type == TempStreamResultType.NeedNewStream);
            return result;
        }

        public TempStreamWriter GetWriter()
        {
            return _serviceProvider.GetRequiredService<TempStreamWriter>();
        }
    }
}
