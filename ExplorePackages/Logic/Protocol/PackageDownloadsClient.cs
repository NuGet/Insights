using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Knapcode.ExplorePackages.Support;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages.Logic
{
    public class PackageDownloadsClient
    {
        private readonly HttpClient _httpClient;
        private readonly ExplorePackagesSettings _settings;

        public PackageDownloadsClient(
            HttpClient httpClient,
            ExplorePackagesSettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;
        }

        public IAsyncEnumerable<PackageDownloads> GetPackageDownloads()
        {
            if (_settings.DownloadsV1Url == null)
            {
                throw new InvalidOperationException("The downloads.v1.json URL is required.");
            }

            return new PackageDownloadsAsyncEnumerable(_httpClient, _settings);
        }

        private class PackageDownloadsAsyncEnumerable : IAsyncEnumerable<PackageDownloads>
        {
            private readonly HttpClient _httpClient;
            private readonly ExplorePackagesSettings _settings;

            public PackageDownloadsAsyncEnumerable(
                HttpClient httpClient,
                ExplorePackagesSettings settings)
            {
                _httpClient = httpClient;
                _settings = settings;
            }

            public IAsyncEnumerator<PackageDownloads> GetEnumerator()
            {
                return new PackageDownloadsAsyncEnumerator(_httpClient, _settings);
            }
        }

        private class PackageDownloadsAsyncEnumerator : IAsyncEnumerator<PackageDownloads>
        {
            private readonly HttpClient _httpClient;
            private readonly ExplorePackagesSettings _settings;

            private Stack<IDisposable> _disposables;
            private JsonTextReader _jsonReader;

            private State _state;
            private string _currentId;
            private PackageDownloads _current;

            public PackageDownloadsAsyncEnumerator(
                HttpClient httpClient,
                ExplorePackagesSettings settings)
            {
                _httpClient = httpClient;
                _settings = settings;
                _disposables = new Stack<IDisposable>();
                _state = State.Uninitialized;
            }

            public PackageDownloads Current => _current;

            public void Dispose()
            {
                while (_disposables.Any())
                {
                    _disposables.Pop()?.Dispose();
                }
            }

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                _current = null;
                while (_current == null && _state != State.Complete)
                {
                    switch (_state)
                    {
                        case State.Uninitialized:
                            await InitializeJsonReaderAsync();
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

            private async Task InitializeJsonReaderAsync()
            {
                using (var request = new HttpRequestMessage(HttpMethod.Get, _settings.DownloadsV1Url))
                {
                    var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                    _disposables.Push(response);

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        response.Dispose();
                        throw new HttpRequestException($"Response status code is not 200 OK: {((int)response.StatusCode)} ({response.ReasonPhrase})");
                    }

                    var stream = await response.Content.ReadAsStreamAsync();
                    _disposables.Push(stream);

                    var textReader = new StreamReader(stream);
                    _disposables.Push(textReader);

                    _jsonReader = new JsonTextReader(textReader);
                    _disposables.Push(_jsonReader);
                }
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
