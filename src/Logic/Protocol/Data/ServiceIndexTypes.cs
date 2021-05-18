// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public static class ServiceIndexTypes
    {
        public const string FlatContainer = "PackageBaseAddress/3.0.0";
        public const string RegistrationOriginal = "RegistrationsBaseUrl";
        public const string RegistrationGzipped = "RegistrationsBaseUrl/3.4.0";
        public const string RegistrationSemVer2 = "RegistrationsBaseUrl/3.6.0";
        public const string V2Search = "SearchGalleryQueryService/3.0.0-rc";
        public const string Autocomplete = "SearchAutocompleteService";
        public const string Catalog = "Catalog/3.0.0";
    }
}
