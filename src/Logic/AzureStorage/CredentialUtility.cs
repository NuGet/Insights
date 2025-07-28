using Azure.Identity;
using Azure.Core;

namespace NuGet.Insights
{
    public static class CredentialUtility
    {
        public static TokenCredential GetDefaultAzureCredential()
        {
#if DEBUG
            return new DefaultAzureCredential();
#else
            throw new NotSupportedException("DefaultAzureCredential is not supported in production. Use a different credential type.");
#endif
        }
    }
}
