using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Knapcode.ExplorePackages
{
    public class DownloadsV1JsonDeserializer
    {
        public IAsyncEnumerable<PackageDownloads> DeserializeAsync(TextReader reader, Stack<IDisposable> disposables, IThrottle throttle)
        {
            var jsonReader = new JsonTextReader(reader);
            disposables.Push(jsonReader);

            return new PackageDownloadsAsyncEnumerable(new PackageDownloadsAsyncEnumerator(
                jsonReader,
                disposables,
                throttle));
        }

        private class PackageDownloadsAsyncEnumerable : IAsyncEnumerable<PackageDownloads>
        {
            private readonly IAsyncEnumerator<PackageDownloads> _enumerator;

            public PackageDownloadsAsyncEnumerable(IAsyncEnumerator<PackageDownloads> enumerator)
            {
                _enumerator = enumerator;
            }

            public IAsyncEnumerator<PackageDownloads> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return _enumerator;
            }
        }

        private class PackageDownloadsAsyncEnumerator : IAsyncEnumerator<PackageDownloads>
        {
            private readonly JsonReader _jsonReader;
            private readonly Stack<IDisposable> _disposables;
            private readonly IThrottle _throttle;

            private State _state;
            private string _currentId;
            private int _disposed;

            public PackageDownloadsAsyncEnumerator(
                JsonReader jsonReader,
                Stack<IDisposable> disposables,
                IThrottle throttle)
            {
                _jsonReader = jsonReader;
                _disposables = disposables;
                _throttle = throttle;
                _state = State.Uninitialized;
            }

            public PackageDownloads Current { get; private set; }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 0)
                {
                    _throttle.Release();
                }

                while (_disposables.Any())
                {
                    _disposables.Pop()?.Dispose();
                }
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return default;
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                Current = null;
                while (Current == null && _state != State.Complete)
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
                                Current = new PackageDownloads(_currentId, version, downloads);
                            }
                            else
                            {
                                throw new JsonReaderException("Error reading start array or end array.");
                            }
                            break;
                    }
                }

                return Current != null;
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
