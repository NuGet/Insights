// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class BlobContainerStorageSettings
    {
        public BlobContainerStorageSettings(string name)
        {
            Name = name;
        }

        public BlobContainerStorageSettings()
        {
        }

        /// <summary>
        /// The blob container name.
        /// </summary>
        public string Name { get; set; }
        public string StorageAccountName { get; set; } = null;
    }
}
