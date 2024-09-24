// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Insights
{
    public class RealStorageTokenCredentialFactAttribute : NoInMemoryStorageFactAttribute
    {
        public RealStorageTokenCredentialFactAttribute()
        {
            if (Skip is null)
            {
                var type = LogicTestSettings.StorageCredentialType;
                switch (type)
                {
                    case StorageCredentialType.ClientCertificateCredentialFromKeyVault:
                    case StorageCredentialType.ClientCertificateCredentialFromPath:
                    case StorageCredentialType.UserAssignedManagedIdentityCredential:
                    case StorageCredentialType.DefaultAzureCredential:
                        break;
                    default:
                        Skip = $"This Fact is skipped because the current storage credential is not a token credential. The credential type is {type}.";
                        break;
                }
            }
        }
    }
}
