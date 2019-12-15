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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grpc.AspNetCore.Web.Internal
{
    internal sealed class GrpcWebMiddleware
    {
        private readonly GrpcWebOptions _options;
        private readonly ILogger<GrpcWebMiddleware> _logger;
        private readonly RequestDelegate _next;

        public GrpcWebMiddleware(IOptions<GrpcWebOptions> options, ILogger<GrpcWebMiddleware> logger, RequestDelegate next)
        {
            _options = options.Value;
            _logger = logger;
            _next = next;
        }

        public Task Invoke(HttpContext httpContext)
        {
            var mode = GetGrpcWebMode(httpContext);
            if (mode != GrpcWebMode.None)
            {
                Log.DetectedGrpcWebRequest(_logger, httpContext.Request.ContentType);

                var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<IGrpcWebEnabledMetadata>();
                if (metadata?.GrpcWebEnabled ?? _options.GrpcWebEnabled)
                {
                    return HandleGrpcWebRequest(httpContext, mode);
                }

                Log.GrpcWebRequestNotProcessed(_logger);
            }

            return _next(httpContext);
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
            httpContext.Request.Protocol = GrpcWebProtocolConstants.Http2Protocol;
            httpContext.Request.ContentType = ResolveContentType(GrpcWebProtocolConstants.GrpcContentType, httpContext.Request.ContentType);

            // Update response content type back to gRPC-Web
            httpContext.Response.OnStarting(() =>
            {
                var contentType = mode == GrpcWebMode.GrpcWeb
                    ? GrpcWebProtocolConstants.GrpcWebContentType
                    : GrpcWebProtocolConstants.GrpcWebTextContentType;
                var responseContentType = ResolveContentType(contentType, httpContext.Response.ContentType);

                httpContext.Response.ContentType = responseContentType;
                Log.SendingGrpcWebResponse(_logger, responseContentType);
                
                return Task.CompletedTask;
            });

            await _next(httpContext);

            if (trailersFeature.Trailers.Count > 0)
            {
                await GrpcWebProtocolHelpers.WriteTrailers(trailersFeature.Trailers, httpContext.Response.BodyWriter);
            }
        }

        private static string ResolveContentType(string newContentType, string originalContentType)
        {
            var contentSuffixIndex = originalContentType.IndexOf('+', StringComparison.Ordinal);
            if (contentSuffixIndex != -1)
            {
                newContentType += originalContentType.Substring(contentSuffixIndex);
            }

            return newContentType;
        }

        internal static GrpcWebMode GetGrpcWebMode(HttpContext httpContext)
        {
            if (httpContext.Request.Method == HttpMethods.Post)
            {
                if (IsContentType(GrpcWebProtocolConstants.GrpcWebContentType, httpContext.Request.ContentType))
                {
                    return GrpcWebMode.GrpcWeb;
                }
                else if (IsContentType(GrpcWebProtocolConstants.GrpcWebTextContentType, httpContext.Request.ContentType))
                {
                    return GrpcWebMode.GrpcWebText;
                }
            }
            
            return GrpcWebMode.None;
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

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception?> _detectedGrpcWebRequest =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "DetectedGrpcWebRequest"), "Detected gRPC-Web request from content-type '{ContentType}'.");

            private static readonly Action<ILogger, Exception?> _grpcWebRequestNotProcessed =
                LoggerMessage.Define(LogLevel.Debug, new EventId(2, "GrpcWebRequestNotProcessed"), "gRPC-Web request not processed.");

            private static readonly Action<ILogger, string, Exception?> _sendingGrpcWebResponse =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3, "SendingGrpcWebResponse"), "Sending gRPC-Web response with content-type '{ContentType}'.");

            public static void DetectedGrpcWebRequest(ILogger<GrpcWebMiddleware> logger, string contentType)
            {
                _detectedGrpcWebRequest(logger, contentType, null);
            }

            public static void GrpcWebRequestNotProcessed(ILogger<GrpcWebMiddleware> logger)
            {
                _grpcWebRequestNotProcessed(logger, null);
            }

            public static void SendingGrpcWebResponse(ILogger<GrpcWebMiddleware> logger, string contentType)
            {
                _sendingGrpcWebResponse(logger, contentType, null);
            }
        }
    }
}
