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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client.Web.Internal;

namespace Grpc.Net.Client.Web
{
    /// <summary>
    /// A <see cref="DelegatingHandler"/> implementation that executes gRPC-Web request processing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This message handler implementation should be used with the .NET client for gRPC to make gRPC-Web calls.
    /// </para>
    /// </remarks>
    public sealed class GrpcWebHandler : DelegatingHandler
    {
        private readonly GrpcWebMode _mode;
        private readonly Version _httpVersion;

        /// <summary>
        /// Creates a new instance of <see cref="GrpcWebHandler"/>.
        /// </summary>
        /// <param name="mode">The gRPC-Web mode to use when making gRPC-Web calls.</param>
        public GrpcWebHandler(GrpcWebMode mode) : this(mode, GrpcWebProtocolConstants.Http20)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="GrpcWebHandler"/>.
        /// </summary>
        /// <param name="mode">The gRPC-Web mode to use when making gRPC-Web calls.</param>
        /// <param name="httpVersion">The HTTP version to used when making gRPC-Web calls.</param>
        public GrpcWebHandler(GrpcWebMode mode, Version httpVersion)
        {
            if (httpVersion == null)
            {
                throw new ArgumentNullException(nameof(httpVersion));
            }

            _mode = mode;
            _httpVersion = httpVersion;
        }

        /// <summary>
        /// Sends an HTTP request to the inner handler to send to the server as an asynchronous operation.
        /// </summary>
        /// <param name="request">The HTTP request message to send to the server.</param>
        /// <param name="cancellationToken">A cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (IsGrpcContentType(request.Content?.Headers.ContentType))
            {
                return SendAsyncCore(request, cancellationToken);
            }

            return base.SendAsync(request, cancellationToken);
        }

        private async Task<HttpResponseMessage> SendAsyncCore(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Content = new GrpcWebRequestContent(request.Content, _mode);
            request.Version = _httpVersion;

            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            response.Content = new GrpcWebResponseContent(response.Content, _mode, response);
            response.Version = GrpcWebProtocolConstants.Http20;

            return response;
        }

        private static bool IsGrpcContentType(MediaTypeHeaderValue? contentType)
        {
            if (contentType == null)
            {
                return false;
            }

            if (!contentType.MediaType.StartsWith(GrpcWebProtocolConstants.GrpcHeaderValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (contentType.MediaType.Length == GrpcWebProtocolConstants.GrpcHeaderValue.Length)
            {
                // Exact match
                return true;
            }

            // Support variations on the content-type (e.g. +proto, +json)
            var nextChar = contentType.MediaType[GrpcWebProtocolConstants.GrpcHeaderValue.Length];
            if (nextChar == '+')
            {
                // Accept any message format. Marshaller could be set to support third-party formats
                return true;
            }

            return false;
        }
    }
}
