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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Grpc.AspNetCore.Web.Internal
{
    internal sealed class GrpcWebMiddleware
    {
        private readonly RequestDelegate _next;

        public GrpcWebMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public Task Invoke(HttpContext httpContext)
        {
            var mode = GetGrpcWebMode(httpContext);
            if (mode == GrpcWebMode.None)
            {
                return _next(httpContext);
            }

            return HandleGrpcWebRequest(httpContext, mode);
        }

        private async Task HandleGrpcWebRequest(HttpContext httpContext, GrpcWebMode mode)
        {
            var trailersFeature = new GrpcWebResponseTrailersFeature();
            httpContext.Features.Set<IHttpResponseTrailersFeature>(trailersFeature);

            if (mode == GrpcWebMode.GrpcWebText)
            {
                var grpcWebTextFeature = new GrpcWebTextFeature(httpContext);
                httpContext.Features.Set<IRequestBodyPipeFeature>(grpcWebTextFeature);
                httpContext.Features.Set<IHttpResponseBodyFeature>(grpcWebTextFeature);
            }

            // Modifying the request is required to stop Grpc.AspNetCore.Server from rejecting it
            httpContext.Request.Protocol = GrpcProtocolConstants.Http2Protocol;
            httpContext.Request.ContentType = GrpcProtocolConstants.GrpcContentType;

            // Update response content type back to gRPC-Web
            httpContext.Response.OnStarting(() =>
            {
                var contentType = mode == GrpcWebMode.GrpcWeb
                    ? GrpcProtocolConstants.GrpcWebContentType
                    : GrpcProtocolConstants.GrpcWebTextContentType;

                httpContext.Response.ContentType = contentType;
                return Task.CompletedTask;
            });

            await _next(httpContext);

            if (trailersFeature.Trailers.Count > 0)
            {
                await GrpcWebProtocolHelpers.WriteTrailers(trailersFeature.Trailers, httpContext.Response.BodyWriter);
            }
        }

        private static GrpcWebMode GetGrpcWebMode(HttpContext httpContext)
        {
            if (IsContentType(GrpcProtocolConstants.GrpcWebContentType, httpContext.Request.ContentType))
            {
                return GrpcWebMode.GrpcWeb;
            }
            else if (IsContentType(GrpcProtocolConstants.GrpcWebTextContentType, httpContext.Request.ContentType))
            {
                return GrpcWebMode.GrpcWebText;
            }
            
            return GrpcWebMode.None;
        }

        private enum GrpcWebMode
        {
            None,
            GrpcWeb,
            GrpcWebText
        }

        private static bool IsContentType(string contentType, string s)
        {
            if (s == null)
            {
                return false;
            }

            if (!s.StartsWith(contentType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (s.Length == contentType.Length)
            {
                // Exact match
                return true;
            }

            // Support variations on the content-type (e.g. +proto, +json)
            char nextChar = s[contentType.Length];
            if (nextChar == ';')
            {
                return true;
            }
            if (nextChar == '+')
            {
                // Accept any message format. Marshaller could be set to support third-party formats
                return true;
            }

            return false;
        }
    }
}
