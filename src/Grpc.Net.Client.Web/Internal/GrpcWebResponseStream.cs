#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Grpc.Net.Client.Web.Internal
{
    internal class GrpcWebResponseStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _httpResponseMessage;
        private int _contentRemaining;
        private ResponseState _state;

        public GrpcWebResponseStream(Stream inner, HttpResponseMessage httpResponseMessage)
        {
            _inner = inner;
            _httpResponseMessage = httpResponseMessage;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> data, CancellationToken cancellationToken = default)
        {
            switch (_state)
            {
                case ResponseState.Ready:
                    // Read the header first
                    // - 1 byte flag for compression
                    // - 4 bytes for the content length
                    Memory<byte> headerBuffer;

                    if (data.Length >= 5)
                    {
                        headerBuffer = data.Slice(0, 5);
                    }
                    else
                    {
                        // Should never get here. Client always passes 5 to read the header.
                        throw new InvalidOperationException("Buffer is not large enough for header");
                    }

                    var headerDetails = await ReadHeaderAsync(_inner, headerBuffer, cancellationToken).ConfigureAwait(false);
                    if (headerDetails == null)
                    {
                        return 0;
                    }

                    _contentRemaining = (int)headerDetails.Value.length;

                    var isTrailer = IsBitSet(headerDetails.Value.compressed, pos: 7);
                    if (isTrailer)
                    {
                        return await ParseTrailer();
                    }

                    // If there is no content then state is still ready
                    _state = _contentRemaining > 0 ? ResponseState.Content : ResponseState.Ready;
                    return 5;
                case ResponseState.Content:
                    if (data.Length >= _contentRemaining)
                    {
                        data = data.Slice(0, _contentRemaining);
                    }

                    var read = await _inner.ReadAsync(data, cancellationToken);
                    _contentRemaining -= read;
                    if (_contentRemaining == 0)
                    {
                        _state = ResponseState.Ready;
                    }

                    return read;
                default:
                    throw new InvalidOperationException("Unexpected state.");
            }
        }

        private async ValueTask<int> ParseTrailer()
        {
            var sr = new StreamReader(_inner, Encoding.ASCII);

            string? line;
            while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    var delimiter = line.IndexOf(':', StringComparison.Ordinal);
                    var name = line.Substring(0, delimiter);
                    var value = line.Substring(delimiter + 1).Trim();

                    _httpResponseMessage.TrailingHeaders.Add(name, value);
                }
            }

            _state = ResponseState.Complete;
            return 0;
        }

        private static bool IsBitSet(byte b, int pos)
        {
            return ((b >> pos) & 1) != 0;
        }

        private static async Task<(uint length, byte compressed)?> ReadHeaderAsync(Stream responseStream, Memory<byte> header, CancellationToken cancellationToken)
        {
            int read;
            var received = 0;
            while ((read = await responseStream.ReadAsync(header.Slice(received, header.Length - received), cancellationToken).ConfigureAwait(false)) > 0)
            {
                received += read;

                if (received == header.Length)
                {
                    break;
                }
            }

            if (received < header.Length)
            {
                if (received == 0)
                {
                    return null;
                }

                throw new InvalidDataException("Unexpected end of content while reading the message header.");
            }

            var compressed = header.Span[0];
            var length = BinaryPrimitives.ReadUInt32BigEndian(header.Span.Slice(1));

            return (length, compressed);
        }

        private enum ResponseState
        {
            Ready,
            Content,
            Complete
        }

        #region Stream implementation
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position
        {
            get => _inner.Position;
            set { _inner.Position = value; }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
