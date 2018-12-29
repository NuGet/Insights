using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDownloadsClient : IPackageDownloadsClient
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsSnapshot<ExplorePackagesSettings> _settings;

        public PackageDownloadsClient(
            HttpClient httpClient,
            IOptionsSnapshot<ExplorePackagesSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings;
        }

        public async Task<PackageDownloadSet> GetPackageDownloadSetAsync(string etag)
        {
            if (_settings.Value.DownloadsV1Url == null)
            {
                throw new InvalidOperationException("The downloads.v1.json URL is required.");
            }

            var disposables = new Stack<IDisposable>();
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _settings.Value.DownloadsV1Url);
                disposables.Push(request);

                // Prior to this version, Azure Blob Storage did not put quotes around etag headers...
                request.Headers.TryAddWithoutValidation("x-ms-version", "2017-04-17");

                if (etag != null)
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", etag);
                }

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                disposables.Push(response);

                string newEtag;
                TextReader textReader;                
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    newEtag = response.Headers.ETag.ToString();

                    var stream = await response.Content.ReadAsStreamAsync();
                    disposables.Push(stream);

                    textReader = new StreamReader(stream);
                    disposables.Push(textReader);
                }
                else if (etag != null && response.StatusCode == HttpStatusCode.NotModified)
                {
                    newEtag = etag;

                    textReader = new StringReader("[]");
                    disposables.Push(textReader);
                }
                else
                {
                    response.Dispose();
                    throw new HttpRequestException($"Response status code is not 200 OK: {((int)response.StatusCode)} ({response.ReasonPhrase})");
                }

                var jsonReader = new JsonTextReader(textReader);
                disposables.Push(jsonReader);

                return new PackageDownloadSet(
                    newEtag,
                    new PackageDownloadsAsyncEnumerator(
                        jsonReader,
                        disposables));
            }
            catch
            {
                DisposeAll(disposables);
                throw;
            }
        }

        private static void DisposeAll(Stack<IDisposable> disposables)
        {
            while (disposables.Any())
            {
                disposables.Pop()?.Dispose();
            }
        }

        private class PackageDownloadsAsyncEnumerator : IAsyncEnumerator<PackageDownloads>
        {
            private Stack<IDisposable> _disposables;
            private JsonReader _jsonReader;

            private State _state;
            private string _currentId;
            private PackageDownloads _current;

            public PackageDownloadsAsyncEnumerator(
                JsonReader jsonReader,
                Stack<IDisposable> disposables)
            {
                _jsonReader = jsonReader;
                _disposables = disposables;
                _state = State.Uninitialized;
            }

            public PackageDownloads Current => _current;

            public void Dispose()
            {
                DisposeAll(_disposables);
            }

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                _current = null;
                while (_current == null && _state != State.Complete)
                {
                    switch (_state)
                    {
                        case State.Uninitialized:
                            await _jsonReader.ReadAsStartArrayAsync();
                            _state = State.InRootArray;
                            break;

                        case State.InRootArray:
                            await _jsonReader.ReadRequiredAsync();
                            if (_jsonReader.TokenType == JsonToken.EndArray)
                            {
                                _state = State.Complete;
                                Dispose();
                            }
                            else if (_jsonReader.TokenType == JsonToken.StartArray)
                            {
                                _currentId = await _jsonReader.ReadAsStringAsync();
                                _state = State.InRegistrationArray;
                            }
                            else
                            {
                                throw new JsonReaderException("Error reading start array or end array.");
                            }
                            break;

                        case State.InRegistrationArray:
                            await _jsonReader.ReadRequiredAsync();
                            if (_jsonReader.TokenType == JsonToken.EndArray)
                            {
                                _state = State.InRootArray;
                            }
                            else if (_jsonReader.TokenType == JsonToken.StartArray)
                            {
                                var version = await _jsonReader.ReadAsStringAsync();
                                var downloads = await _jsonReader.ReadAsInt64Async();
                                await _jsonReader.ReadAsEndArrayAsync();
                                _current = new PackageDownloads(_currentId, version, downloads);
                            }
                            else
                            {
                                throw new JsonReaderException("Error reading start array or end array.");
                            }
                            break;
                    }
                }

                return _current != null;
            }

            private enum State
            {
                Uninitialized,
                InRootArray,
                InRegistrationArray,
                Complete,
            }
        }
    }
}
