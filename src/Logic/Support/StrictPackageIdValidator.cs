using System.Text.RegularExpressions;

namespace NuGet.Insights
{
    /// <summary>
    /// Sources:
    /// <see cref="https://github.com/NuGet/NuGet2/blob/e89c4b2fc6dc2fc752d2fceecd2a2175b0f4ab69/src/Core/Utility/PackageIdValidator.cs"/>
    /// <see cref="https://github.com/NuGet/NuGet.Client/blob/572962f1b1ee890533e5a75163264bd021426dc7/src/NuGet.Core/NuGet.Packaging/PackageCreation/Utility/PackageIdValidator.cs"/>
    /// </summary>
    public static class StrictPackageIdValidator
    {
        private const int MaxPackageIdLength = 100;
        private static readonly Regex IdRegex = new Regex(@"^\w+([_.-]\w+)*$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

        public static bool IsValid(string id)
        {
            return id != null
                && id.Length <= MaxPackageIdLength
                && IdRegex.IsMatch(id);
        }
    }
}
