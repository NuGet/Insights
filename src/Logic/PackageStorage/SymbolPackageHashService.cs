// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

namespace NuGet.Insights
{
    public class SymbolPackageHashService : PackageSpecificHashService
    {
        private readonly IOptions<NuGetInsightsSettings> _options;


        public SymbolPackageHashService(
            PackageWideEntityService packageWideEntityService,
            IOptions<NuGetInsightsSettings> options) : base(packageWideEntityService)
        {
            _options = options;
        }

        protected override string TableName => _options.Value.SymbolPackageHashesTable;
        protected override bool MissingHashesIsDeleted => false;
    }
}
