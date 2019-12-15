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
using Grpc.AspNetCore.Web.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>
    /// Provides extension methods for <see cref="IApplicationBuilder"/> to add gRPC-Web middleware.
    /// </summary>
    public static class GrpcWebApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds gRPC-Web middleware to the specified <see cref="IApplicationBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IApplicationBuilder UseGrpcWeb(this IApplicationBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            ValidateServicesRegistered(builder.ApplicationServices);

            return builder.UseMiddleware<GrpcWebMiddleware>();
        }

        private static void ValidateServicesRegistered(IServiceProvider serviceProvider)
        {
            // Verify that AddGrpcWeb was called before calling UseGrpcWeb
            // We use the GrpcWebMarkerService to ensure all the services were added.
            var marker = serviceProvider.GetService(typeof(GrpcWebMarkerService));
            if (marker == null)
            {
                throw new InvalidOperationException("Unable to find the required services. Please add all the required services by calling " +
                    "'IServiceCollection.AddGrpcWeb' inside the call to 'ConfigureServices(...)' in the application startup code.");
            }
        }
    }
}
