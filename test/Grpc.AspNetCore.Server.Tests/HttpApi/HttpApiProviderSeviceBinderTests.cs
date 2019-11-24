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
using System.Threading.Tasks;
using Greet;
using Grpc.AspNetCore.Server.HttpApi;
using Grpc.AspNetCore.Server.Model;
using Grpc.AspNetCore.Server.Tests.TestObjects;
using Grpc.Core;
using Grpc.Tests.Shared;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Grpc.AspNetCore.Server.Tests.HttpApi
{
    [TestFixture]
    public class HttpApiProviderSeviceBinderTests
    {
        [Test]
        public void AddMethod_OptionGet_ResolveMethod()
        {
            // Arrange
            var context = RunBinder<HttpApiGreeterService>(binder =>
            {
                // Act
                var method = MessageHelpers.CreateServiceMethod(nameof(HttpApiGreeterService.SayHello));
                binder.AddMethod(method, DummyInvokeMethod);
            });

            // Assert
            Assert.AreEqual(1, context.Methods.Count);
            var methodModel = context.Methods[0];
            Assert.AreEqual("GET", methodModel.Metadata.OfType<IHttpMethodMetadata>().Single().HttpMethods.Single());
            Assert.AreEqual("/v1/greeter/{name}", methodModel.Pattern);
        }

        [Test]
        public void AddMethod_OptionCustom_ResolveMethod()
        {
            // Arrange
            var context = RunBinder<HttpApiGreeterService>(binder =>
            {
                // Act
                var method = MessageHelpers.CreateServiceMethod(nameof(HttpApiGreeterService.Custom));
                binder.AddMethod(method, DummyInvokeMethod);
            });

            // Assert
            Assert.AreEqual(1, context.Methods.Count);
            var methodModel = context.Methods[0];
            Assert.AreEqual("HEAD", methodModel.Metadata.OfType<IHttpMethodMetadata>().Single().HttpMethods.Single());
            Assert.AreEqual("/v1/greeter/{name}", methodModel.Pattern);
        }

        [Test]
        public void AddMethod_OptionAdditionalBindings_ResolveMethods()
        {
            // Arrange
            var context = RunBinder<HttpApiGreeterService>(binder =>
            {
                // Act
                var method = MessageHelpers.CreateServiceMethod(nameof(HttpApiGreeterService.AdditionalBindings));
                binder.AddMethod(method, DummyInvokeMethod);
            });

            // Assert
            Assert.AreEqual(2, context.Methods.Count);
            
            var getMethodModel = context.Methods[0];
            Assert.AreEqual("GET", getMethodModel.Metadata.OfType<IHttpMethodMetadata>().Single().HttpMethods.Single());
            Assert.AreEqual("/v1/additional_bindings/{name}", getMethodModel.Pattern);

            var additionalMethodModel = context.Methods[1];
            Assert.AreEqual("DELETE", additionalMethodModel.Metadata.OfType<IHttpMethodMetadata>().Single().HttpMethods.Single());
            Assert.AreEqual("/v1/additional_bindings/{name}", additionalMethodModel.Pattern);
        }

        [Test]
        public void AddMethod_NoOption_ResolveMethod()
        {
            // Arrange
            var context = RunBinder<HttpApiGreeterService>(binder =>
            {
                // Act
                var method = MessageHelpers.CreateServiceMethod(nameof(HttpApiGreeterService.NoOption));
                binder.AddMethod(method, DummyInvokeMethod);
            });

            // Assert
            Assert.AreEqual(1, context.Methods.Count);
            var methodModel = context.Methods[0];
            Assert.AreEqual("GET", methodModel.Metadata.OfType<IHttpMethodMetadata>().Single().HttpMethods.Single());
            Assert.AreEqual("/ServiceName/NoOption", methodModel.Pattern);
        }

        [Test]
        public void AddMethod_BadResponseBody_ResolveMethod()
        {
            // Arrange
            RunBinder<HttpApiInvalidResponseBodyGreeterService>(binder =>
            {
                // Act
                var method = MessageHelpers.CreateServiceMethod(nameof(HttpApiInvalidResponseBodyGreeterService.BadResponseBody));
                var ex = Assert.Throws<InvalidOperationException>(() => binder.AddMethod(method, DummyInvokeMethod));

                // Assert
                Assert.AreEqual("Error binding BadResponseBody on HttpApiInvalidResponseBodyGreeterService to HTTP API.", ex.Message);
                Assert.AreEqual("Couldn't find matching field for response body 'NoMatch' on HelloReply.", ex.InnerException!.Message);
            });
        }

        private ServiceMethodProviderContext<TService> RunBinder<TService>(
            //Google.Protobuf.Reflection.ServiceDescriptor serviceDescriptor,
            Action<HttpApiProviderServiceBinder<TService>> bind)
            where TService : class
        {
            var serviceCollection = new ServiceCollection();

            var context = new ServiceMethodProviderContext<TService>(
                new Internal.ServerCallHandlerFactory<TService>(
                    NullLoggerFactory.Instance,
                    Options.Create(new GrpcServiceOptions()),
                    Options.Create(new GrpcServiceOptions<TService>()),
                    serviceCollection.BuildServiceProvider()));

            var bindMethodInfo = BindMethodFinder.GetBindMethod(typeof(TService))!;
            var serviceDescriptor = ServiceDescriptorHelpers.GetServiceDescriptor(bindMethodInfo.DeclaringType!)!;

            var binder = new HttpApiProviderServiceBinder<TService>(
                context,
                typeof(TService).BaseType!, // TODO: Make parameter
                serviceDescriptor,
                new GrpcServiceOptions(),
                new GrpcServiceOptions<TService>(),
                serviceCollection.BuildServiceProvider(),
                NullLoggerFactory.Instance);

            bind(binder);

            return context;
        }

        private Task<HelloReply> DummyInvokeMethod(HelloRequest request, ServerCallContext context)
        {
            return Task.FromResult(new HelloReply());
        }
    }
}
