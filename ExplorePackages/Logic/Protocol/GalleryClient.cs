using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Parser.Html;
using NuGet.Common;
using NuGet.Protocol;
using NuGet.Versioning;

namespace Knapcode.ExplorePackages.Logic
{
    public class GalleryClient
    {
        private static readonly Regex FlattenWhitespace = new Regex(@"\s+");
        private readonly HttpSource _httpSource;
        private readonly ILogger _log;

        public GalleryClient(HttpSource httpSource, ILogger log)
        {
            _httpSource = httpSource;
            _log = log;
        }

        public async Task<PackageDeletedStatus> GetPackageDeletedStatusAsync(string baseUrl, string id, string version)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersion = parsedVersion.ToNormalizedString();
            var packageIdentity = new PackageIdentity(id, normalizedVersion);
            var url = $"{baseUrl.TrimEnd('/')}/packages/{id}/{normalizedVersion}";

            var packageDeletedStatus = await _httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, _log)
                {
                    IgnoreNotFounds = true,
                },
                async stream =>
                {
                    if (stream == null)
                    {
                        return PackageDeletedStatus.Unknown;
                    }

                    var buffer = new byte[8 * 1024];
                    var responseBody = new MemoryStream(buffer.Length);
                    var desiredBytes = buffer.Length;
                    int read;
                    do
                    {
                        read = await stream.ReadAsync(buffer, 0, buffer.Length);
                        responseBody.Write(buffer, 0, read);
                        responseBody.Position = 0;

                        var status = DeterminePackageDeletedStatus(packageIdentity, responseBody);
                        if (status.HasValue)
                        {
                            return status.Value;
                        }

                        desiredBytes += buffer.Length;
                    }
                    while (responseBody.Length < desiredBytes && read > 0);

                    throw new InvalidDataException($"The package deleted status could not be determined at {url}.");
                },
                _log,
                CancellationToken.None);

            return packageDeletedStatus;
        }

        private PackageDeletedStatus? DeterminePackageDeletedStatus(PackageIdentity packageIdentity, MemoryStream responseBody)
        {
            var parser = new HtmlParser();
            var document = parser.Parse(responseBody);

            var metaTitleEl = document.Head.QuerySelector("meta[property='og:title']");
            if (metaTitleEl == null)
            {
                return null;
            }

            var metaTitle = metaTitleEl.GetAttribute("content");
            if (metaTitle == null)
            {
                return null;
            }

            var pieces = metaTitle.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length < 2)
            {
                return null;
            }

            var foundPackageIdentity = new PackageIdentity(
                pieces[0].Trim(),
                NuGetVersion.Parse(pieces[1]).ToNormalizedString());
            if (!StringComparer.OrdinalIgnoreCase.Equals(packageIdentity.Id, foundPackageIdentity.Id))
            {
                throw new InvalidDataException("The package ID found in the meta title does not match the request.");
            }
            else if (!foundPackageIdentity.Equals(packageIdentity))
            {
                return PackageDeletedStatus.Unknown;
            }

            var dangerAlerts = document.QuerySelectorAll("div.alert-danger");
            foreach (var dangerAlert in dangerAlerts)
            {
                var flattenedText = FlattenWhitespace.Replace(dangerAlert.TextContent.Trim(), " ");
                if (flattenedText.Contains("This package has been deleted from the gallery."))
                {
                    return PackageDeletedStatus.SoftDeleted;
                }
            }

            if (document.QuerySelector("footer") != null)
            {
                return PackageDeletedStatus.NotDeleted;
            }

            return null;
        }
    }
}
