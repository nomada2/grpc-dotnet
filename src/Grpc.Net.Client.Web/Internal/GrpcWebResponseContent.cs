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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Grpc.Net.Client.Web.Internal
{
    internal class GrpcWebResponseContent : HttpContent
    {
        private readonly HttpContent _inner;
        private readonly GrpcWebMode _mode;
        private readonly HttpResponseMessage _httpResponseMessage;

        public GrpcWebResponseContent(HttpContent inner, GrpcWebMode mode, HttpResponseMessage httpResponseMessage)
        {
            _inner = inner;
            _mode = mode;
            _httpResponseMessage = httpResponseMessage;

            foreach (var header in inner.Headers)
            {
                Headers.Add(header.Key, header.Value);
            }

            Headers.ContentType = GrpcWebProtocolConstants.GrpcHeader;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            // This method will only be called by tests when response content is
            // accessed via ReadAsBytesAsync. The gRPC client will always
            // call ReadAsStreamAsync, which will call CreateContentReadStreamAsync.

            var innerStream = await _inner.ReadAsStreamAsync().ConfigureAwait(false);

            if (_mode == GrpcWebMode.GrpcWebText)
            {
                innerStream = new Base64ResponseStream(innerStream);
            }

            innerStream = new GrpcWebResponseStream(innerStream, _httpResponseMessage);

            await innerStream.CopyToAsync(stream).ConfigureAwait(false);
        }

        protected override async Task<Stream> CreateContentReadStreamAsync()
        {
            var stream = await _inner.ReadAsStreamAsync().ConfigureAwait(false);

            //var ms = new MemoryStream();
            //await stream.CopyToAsync(ms).ConfigureAwait(false);
            //ms.Seek(0, SeekOrigin.Begin);

            //var data = ms.ToArray();

            //stream = ms;

            if (_mode == GrpcWebMode.GrpcWebText)
            {
                stream = new Base64ResponseStream(stream);
            }

            return new GrpcWebResponseStream(stream, _httpResponseMessage);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // This is important. Disposing original response content will cancel the gRPC call.
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
