// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights.Worker.KustoIngestion
{
    public class PackageLowerIdKustoValidationProvider : BaseKustoValidationProvider, IKustoValidationProvider
    {
        public PackageLowerIdKustoValidationProvider(CsvRecordContainers containers) : base(containers)
        {
        }

        public async Task<IReadOnlyList<KustoValidation>> GetValidationsAsync()
        {
            return await GetSetValidationsAsync(nameof(PackageRecord.LowerId), required: true);
        }
    }
}
