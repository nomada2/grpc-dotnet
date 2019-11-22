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
using System.Collections.Generic;
using System.IO.Compression;
using Grpc.Core;
using Grpc.Net.Compression;

namespace Grpc.AspNetCore.Server.Model
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class MethodContext
    {
        public Type RequestType { get; }
        public Type ResponseType { get; }
        public Dictionary<string, ICompressionProvider> CompressionProviders { get; }
        public InterceptorCollection Interceptors { get; }
        public int? MaxSendMessageSize { get; }
        public int? MaxReceiveMessageSize { get; }
        public bool? EnableDetailedErrors { get; }
        public string? ResponseCompressionAlgorithm { get; }
        public CompressionLevel? ResponseCompressionLevel { get; }

        // Fast check for whether the service has any interceptors
        internal bool HasInterceptors { get; }

        private MethodContext(
            Type requestType,
            Type responseType,
            Dictionary<string, ICompressionProvider> compressionProviders,
            InterceptorCollection interceptors,
            int? maxSendMessageSize,
            int? maxReceiveMessageSize,
            bool? enableDetailedErrors,
            string? responseCompressionAlgorithm,
            CompressionLevel? responseCompressionLevel)
        {
            RequestType = requestType;
            ResponseType = responseType;
            CompressionProviders = compressionProviders;
            Interceptors = interceptors;
            HasInterceptors = interceptors.Count > 0;
            MaxSendMessageSize = maxSendMessageSize;
            MaxReceiveMessageSize = maxReceiveMessageSize;
            EnableDetailedErrors = enableDetailedErrors;
            ResponseCompressionAlgorithm = responseCompressionAlgorithm;
            ResponseCompressionLevel = responseCompressionLevel;

            if (ResponseCompressionAlgorithm != null)
            {
                if (!CompressionProviders.TryGetValue(ResponseCompressionAlgorithm, out var _))
                {
                    throw new InvalidOperationException($"The configured response compression algorithm '{ResponseCompressionAlgorithm}' does not have a matching compression provider.");
                }
            }
        }

        public static MethodContext Create<TRequest, TResponse>(IEnumerable<GrpcServiceOptions> serviceOptions)
        {
            // This is required to get ensure that service methods without any explicit configuration
            // will continue to get the global configuration options
            var resolvedCompressionProviders = new Dictionary<string, ICompressionProvider>(StringComparer.Ordinal);
            var tempInterceptors = new List<InterceptorRegistration>();
            int? maxSendMessageSize = null;
            int? maxReceiveMessageSize = null;
            bool? enableDetailedErrors = null;
            string? responseCompressionAlgorithm = null;
            CompressionLevel? responseCompressionLevel = null;

            foreach (var options in serviceOptions)
            {
                AddCompressionProviders(resolvedCompressionProviders, options._compressionProviders);
                tempInterceptors.InsertRange(0, options.Interceptors);
                maxSendMessageSize ??= options.MaxSendMessageSize;
                maxReceiveMessageSize ??= options.MaxReceiveMessageSize;
                enableDetailedErrors ??= options.EnableDetailedErrors;
                responseCompressionAlgorithm ??= options.ResponseCompressionAlgorithm;
                responseCompressionLevel ??= options.ResponseCompressionLevel;
            }

            var interceptors = new InterceptorCollection();
            interceptors.AddRange(tempInterceptors);

            return new MethodContext
            (
                requestType: typeof(TRequest),
                responseType: typeof(TResponse),
                compressionProviders: resolvedCompressionProviders,
                interceptors: interceptors,
                maxSendMessageSize: maxSendMessageSize,
                maxReceiveMessageSize: maxReceiveMessageSize,
                enableDetailedErrors: enableDetailedErrors,
                responseCompressionAlgorithm: responseCompressionAlgorithm,
                responseCompressionLevel: responseCompressionLevel
            );
        }

        private static void AddCompressionProviders(Dictionary<string, ICompressionProvider> resolvedProviders, IList<ICompressionProvider>? compressionProviders)
        {
            if (compressionProviders != null)
            {
                foreach (var compressionProvider in compressionProviders)
                {
                    if (!resolvedProviders.ContainsKey(compressionProvider.EncodingName))
                    {
                        resolvedProviders.Add(compressionProvider.EncodingName, compressionProvider);
                    }
                }
            }
        }
    }
}
