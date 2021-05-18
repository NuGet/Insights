// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Html.Parser;
using Microsoft.Extensions.Logging;
using NuGet.Protocol;
using NuGet.Versioning;

namespace NuGet.Insights
{
    public class GalleryClient
    {
        private static readonly Regex FlattenWhitespace = new Regex(@"\s+");
        private readonly HttpSource _httpSource;
        private readonly ILogger<GalleryClient> _logger;

        public GalleryClient(HttpSource httpSource, ILogger<GalleryClient> logger)
        {
            _httpSource = httpSource;
            _logger = logger;
        }

        public async Task<GalleryPackageState> GetPackageStateAsync(string baseUrl, string id, string version)
        {
            var parsedVersion = NuGetVersion.Parse(version);
            var normalizedVersion = parsedVersion.ToNormalizedString();
            var packageIdentity = new PackageIdentity(id, normalizedVersion);
            var url = $"{baseUrl.TrimEnd('/')}/packages/{id}/{normalizedVersion}";

            var nuGetLogger = _logger.ToNuGetLogger();
            return await _httpSource.ProcessStreamAsync(
                new HttpSourceRequest(url, nuGetLogger)
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
                            isListed: false,
                            hasIcon: false);
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

                        var state = DetermineState(baseUrl, packageIdentity, responseBody);
                        if (state != null
                            && state.PackageId != null
                            && state.PackageVersion != null
                            && state.PackageDeletedStatus.HasValue
                            && state.IsListed.HasValue
                            && state.HasIcon.HasValue)
                        {
                            return new GalleryPackageState(
                                state.PackageId,
                                state.PackageVersion,
                                state.PackageDeletedStatus.Value,
                                state.IsListed.Value,
                                state.HasIcon.Value);
                        }

                        desiredBytes += buffer.Length;
                    }
                    while (responseBody.Length < desiredBytes && read > 0);

                    throw new InvalidDataException($"The package state could not be determined at {url}.");
                },
                nuGetLogger,
                CancellationToken.None);
        }

        private MutableState DetermineState(string baseUrl, PackageIdentity packageIdentity, MemoryStream responseBody)
        {
            var state = new MutableState();

            var parser = new HtmlParser();
            var document = parser.ParseDocument(responseBody);

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

            var titlePieces = metaTitle.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (titlePieces.Length < 2)
            {
                return null;
            }

            var foundPackageIdentity = new PackageIdentity(
                titlePieces[0].Trim(),
                NuGetVersion.Parse(titlePieces[1]).ToNormalizedString());

            // Use the found package ID.
            state.PackageId = foundPackageIdentity.Id;

            if (!StringComparer.OrdinalIgnoreCase.Equals(packageIdentity.Id, foundPackageIdentity.Id))
            {
                throw new InvalidDataException("The package ID found in the meta title does not match the request.");
            }
            else if (!foundPackageIdentity.Equals(packageIdentity))
            {
                // Use the input version, since the found one does not match.
                state.PackageVersion = packageIdentity.Version;

                state.PackageDeletedStatus = PackageDeletedStatus.Unknown;
                state.IsListed = false;
                state.HasIcon = false;
                return state;
            }

            // Determine whether the package has an icon
            var metaImageEl = document.Head.QuerySelector("meta[property='og:image']");
            if (metaImageEl == null)
            {
                return null;
            }

            var metaImage = metaImageEl.GetAttribute("content");
            if (metaImage == null)
            {
                return null;
            }

            var defaultIconUrl = baseUrl.TrimEnd('/') + "/Content/gallery/img/default-package-icon-256x256.png";
            state.HasIcon = metaImage != defaultIconUrl;

            // Determine the full version
            var fullVersionEl = document.QuerySelector(".package-details-main .package-title small");
            if (fullVersionEl != null)
            {
                state.PackageVersion = fullVersionEl.TextContent.Trim();
            }

            var alerts = document.QuerySelectorAll("div.alert");
            foreach (var alert in alerts)
            {
                var flattenedText = FlattenWhitespace.Replace(alert.TextContent.Trim(), " ");

                if (flattenedText.Contains("This package has been deleted from the gallery."))
                {
                    state.PackageDeletedStatus = PackageDeletedStatus.SoftDeleted;
                }

                if (flattenedText.Contains("The owner has unlisted this package."))
                {
                    state.IsListed = false;
                }
            }

            if (document.QuerySelector("#version-history") != null)
            {
                state.PackageDeletedStatus = state.PackageDeletedStatus ?? PackageDeletedStatus.NotDeleted;
                state.IsListed = state.IsListed ?? state.PackageDeletedStatus.Value == PackageDeletedStatus.NotDeleted;
            }

            return state;
        }

        private class MutableState
        {
            public string PackageId { get; set; }
            public string PackageVersion { get; set; }
            public PackageDeletedStatus? PackageDeletedStatus { get; set; }
            public bool? IsListed { get; set; }
            public bool? HasIcon { get; set; }
        }
    }
}
