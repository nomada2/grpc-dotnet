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

using System.Collections.Generic;
using System.Threading.Tasks;
using Greet;
using Grpc.AspNetCore.Server.HttpApi;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Grpc.Tests.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using NUnit.Framework;

namespace Grpc.AspNetCore.Server.Tests.HttpApi
{
    [TestFixture]
    public class UnaryServerCallHandlerTests
    {
        [Test]
        public async Task HandleCallAsync_MatchingRouteValue_SetOnRequestMessage()
        {
            // Arrange
            HelloRequest? request = null;
            UnaryServerMethod<HttpApiGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
            {
                request = r;
                return Task.FromResult(new HelloReply());
            };

            var unaryServerCallHandler = CreateCallHandler(invoker);
            var httpContext = CreateHttpContext();
            httpContext.Request.RouteValues["name"] = "TestName!";

            // Act
            await unaryServerCallHandler.HandleCallAsync(httpContext);

            // Assert
            Assert.IsNotNull(request);
            Assert.AreEqual("TestName!", request!.Name);
        }

        [Test]
        public async Task HandleCallAsync_MatchingQueryStringValue_SetOnRequestMessage()
        {
            // Arrange
            HelloRequest? request = null;
            UnaryServerMethod<HttpApiGreeterService, HelloRequest, HelloReply> invoker = (s, r, c) =>
            {
                request = r;
                return Task.FromResult(new HelloReply());
            };

            var unaryServerCallHandler = CreateCallHandler(invoker);
            var httpContext = CreateHttpContext();
            httpContext.Request.Query = new QueryCollection(new Dictionary<string, StringValues>
            {
                ["name"] = "TestName!"
            });

            // Act
            await unaryServerCallHandler.HandleCallAsync(httpContext);

            // Assert
            Assert.IsNotNull(request);
            Assert.AreEqual("TestName!", request!.Name);
        }

        private static DefaultHttpContext CreateHttpContext()
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton<HttpApiGreeterService>();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = serviceProvider;
            return httpContext;
        }

        private static UnaryServerCallHandler<HttpApiGreeterService, HelloRequest, HelloReply> CreateCallHandler(UnaryServerMethod<HttpApiGreeterService, HelloRequest, HelloReply> invoker)
        {
            var serviceCollection = new ServiceCollection();
            var serviceProvider = serviceCollection.BuildServiceProvider();
            var unaryServerCallInvoker = new UnaryMethodInvoker<HttpApiGreeterService, HelloRequest, HelloReply>(
                MessageHelpers.ServiceMethod,
                invoker,
                MethodContext.Create<HelloRequest, HelloReply>(new[] { new GrpcServiceOptions() }),
                serviceProvider);
            var unaryServerCallHandler = new UnaryServerCallHandler<HttpApiGreeterService, HelloRequest, HelloReply>(unaryServerCallInvoker);
            return unaryServerCallHandler;
        }

        private class HttpApiGreeterService : HttpApiGreeter.HttpApiGreeterBase
        {
            public override Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
            {
                return base.SayHello(request, context);
            }
        }
    }
}
