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

        public async Task<GalleryPackageState> GetPackageStateAsync(string baseUrl, string id, string version)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersion = parsedVersion.ToNormalizedString();
            var packageIdentity = new PackageIdentity(id, normalizedVersion);
            var url = $"{baseUrl.TrimEnd('/')}/packages/{id}/{normalizedVersion}";

            return await _httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, _log)
                {
                    IgnoreNotFounds = true,
                },
                async stream =>
                {
                    if (stream == null)
                    {
                        return new GalleryPackageState(
                            packageId: id,
                            packageVersion: version,
                            packageDeletedStatus: PackageDeletedStatus.Unknown,
                            isSemVer2: false,
                            isListed: false);
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

                        var state = DetermineState(packageIdentity, responseBody);
                        if (state.PackageDeletedStatus.HasValue
                            && state.IsSemVer2.HasValue
                            && state.IsListed.HasValue)
                        {
                            return new GalleryPackageState(
                                state.PackageId,
                                state.PackageVersion,
                                state.PackageDeletedStatus.Value,
                                state.IsSemVer2.Value,
                                state.IsListed.Value);
                        }

                        desiredBytes += buffer.Length;
                    }
                    while (responseBody.Length < desiredBytes && read > 0);

                    throw new InvalidDataException($"The package state could not be determined at {url}.");
                },
                _log,
                CancellationToken.None);
        }

        private MutableState DetermineState(PackageIdentity packageIdentity, MemoryStream responseBody)
        {
            var state = new MutableState
            {
                PackageId = packageIdentity.Id,
                PackageVersion = packageIdentity.Version,
            };

            var parser = new HtmlParser();
            var document = parser.Parse(responseBody);

            if (document.QuerySelector("nav") == null)
            {
                return null;
            }

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

            state.PackageId = foundPackageIdentity.Id;
            state.PackageVersion = foundPackageIdentity.Version;

            if (!StringComparer.OrdinalIgnoreCase.Equals(packageIdentity.Id, foundPackageIdentity.Id))
            {
                throw new InvalidDataException("The package ID found in the meta title does not match the request.");
            }
            else if (!foundPackageIdentity.Equals(packageIdentity))
            {
                state.PackageDeletedStatus = PackageDeletedStatus.Unknown;
                state.IsSemVer2 = false;
                state.IsListed = false;
                return state;
            }

            var alerts = document.QuerySelectorAll("div.alert");
            foreach (var alert in alerts)
            {
                var flattenedText = FlattenWhitespace.Replace(alert.TextContent.Trim(), " ");
                
                if (flattenedText.Contains("This package has been deleted from the gallery."))
                {
                    state.PackageDeletedStatus = PackageDeletedStatus.SoftDeleted;
                }

                if (flattenedText.Contains("This package will only be available to download with SemVer 2.0.0 compatible NuGet clients"))
                {
                    state.IsSemVer2 = true;
                }

                if (flattenedText.Contains("The owner has unlisted this package."))
                {
                    state.IsListed = false;
                }
            }

            if (document.QuerySelector("#version-history") != null)
            {
                state.PackageDeletedStatus = state.PackageDeletedStatus ?? PackageDeletedStatus.NotDeleted;
                state.IsSemVer2 = state.IsSemVer2 ?? false;
                state.IsListed = state.IsListed ?? state.PackageDeletedStatus.Value == PackageDeletedStatus.NotDeleted;
            }

            return state;
        }

        private class MutableState
        {
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public PackageDeletedStatus? PackageDeletedStatus { get; set; }
            public bool? IsSemVer2 { get; set; }
            public bool? IsListed { get; set; }
        }
    }
}
