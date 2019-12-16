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
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Grpc.AspNetCore.Web.Internal
{
    internal class GrpcWebFeature : IRequestBodyPipeFeature, IHttpResponseBodyFeature, IHttpResponseTrailersFeature
    {
        private readonly IHttpResponseBodyFeature _initialResponseFeature;
        private readonly IRequestBodyPipeFeature _initialRequestFeature;

        private readonly Base64PipeReader? _pipeReader;
        private readonly Base64PipeWriter? _pipeWriter;
        private IHeaderDictionary _trailers;
        private bool _isComplete;

        public GrpcWebFeature(GrpcWebMode grpcWebMode, HttpContext httpContext)
        {
            _initialRequestFeature = httpContext.Features.Get<IRequestBodyPipeFeature>();
            _initialResponseFeature = httpContext.Features.Get<IHttpResponseBodyFeature>();

            if (grpcWebMode == GrpcWebMode.GrpcWebText)
            {
                _pipeReader = new Base64PipeReader(_initialRequestFeature.Reader);
                _pipeWriter = new Base64PipeWriter(_initialResponseFeature.Writer);
            }

            _trailers = new HeaderDictionary();
        }

        public PipeReader Reader => _pipeReader ?? _initialRequestFeature.Reader;

        public PipeWriter Writer => _pipeWriter ?? _initialResponseFeature.Writer;

        public Stream Stream => _initialResponseFeature.Stream;

        public IHeaderDictionary Trailers
        {
            get => _trailers;
            set { _trailers = value; }
        }

        public async Task CompleteAsync()
        {
            await WriteTrailers();
            await _initialResponseFeature.CompleteAsync();
            _isComplete = true;
        }

        public void DisableBuffering() => _initialResponseFeature.DisableBuffering();

        public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken) =>
            _initialResponseFeature.SendFileAsync(path, offset, count, cancellationToken);

        public Task StartAsync(CancellationToken cancellationToken) =>
            _initialResponseFeature.StartAsync(cancellationToken);

        public Task WriteTrailers()
        {
            if (!_isComplete)
            {
                return GrpcWebProtocolHelpers.WriteTrailers(_trailers, Writer);
            }

            return Task.CompletedTask;
        }
    }
}
