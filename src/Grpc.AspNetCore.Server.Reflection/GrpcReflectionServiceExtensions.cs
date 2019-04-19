﻿#region Copyright notice and license

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
using Grpc.AspNetCore.Server;
using Grpc.AspNetCore.Server.Reflection.Internal;
using Grpc.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Extension methods for the gRPC reflection services.
    /// </summary>
    public static class GrpcReflectionServiceExtensions
    {
        /// <summary>
        /// Adds gRPC reflection services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> for adding services.</param>
        /// <returns>An <see cref="IGrpcServerBuilder"/> that can be used to further configure the gRPC services.</returns>
        public static IServiceCollection AddGrpcReflection(this IServiceCollection services)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            services.TryAddSingleton<IGrpcServiceActivator<ReflectionServiceImpl>, ReflectionGrpcServiceActivator>();

            return services;
        }
    }
}
