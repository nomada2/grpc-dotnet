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
using System.Linq;
using Grpc.AspNetCore.Server.HttpApi;
using Grpc.AspNetCore.Server.Model;
using Grpc.AspNetCore.Server.Tests.TestObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Grpc.AspNetCore.Server.Tests.HttpApi
{
    [TestFixture]
    public class HttpApiServiceMethodProviderTests
    {
        [Test]
        public void OnServiceMethodDiscovery_BadlyConfiguredService_ThrowError()
        {
            // Arrange
            var methodProvider = CreateMethodProvider<HttpApiInvalidResponseBodyGreeterService>();

            var serviceMethodProviderContext = new ServiceMethodProviderContext<HttpApiInvalidResponseBodyGreeterService>(null!);

            // Act
            var ex = Assert.Throws<InvalidOperationException>(() => methodProvider.OnServiceMethodDiscovery(serviceMethodProviderContext));

            // Assert
            Assert.AreEqual("Error binding gRPC service 'HttpApiInvalidResponseBodyGreeterService'.", ex.Message);
            Assert.AreEqual("Error binding BadResponseBody on HttpApiInvalidResponseBodyGreeterService to HTTP API.", ex.InnerException!.InnerException!.Message);
            Assert.AreEqual("Couldn't find matching field for response body 'NoMatch' on HelloReply.", ex.InnerException!.InnerException!.InnerException!.Message);
        }

        [Test]
        public void OnServiceMethodDiscovery_ValidService_ThrowError()
        {
            // Arrange
            var methodProvider = CreateMethodProvider<HttpApiGreeterService>();

            var serviceMethodProviderContext = new ServiceMethodProviderContext<HttpApiGreeterService>(null!);

            // Act
            methodProvider.OnServiceMethodDiscovery(serviceMethodProviderContext);

            // Assert
            Assert.Greater(serviceMethodProviderContext.Methods.Count, 0);
        }

        private static HttpApiServiceMethodProvider<TService> CreateMethodProvider<TService>() where TService : class
        {
            var serviceCollection = new ServiceCollection();
            var methodProvider = new HttpApiServiceMethodProvider<TService>(
                NullLoggerFactory.Instance,
                Options.Create(new GrpcServiceOptions()),
                Options.Create(new GrpcServiceOptions<TService>()),
                serviceCollection.BuildServiceProvider());
            return methodProvider;
        }
    }
}
