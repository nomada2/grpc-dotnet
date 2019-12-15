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
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Grpc.AspNetCore.Server.Tests.Web
{
    [TestFixture]
    public class GrpcWebApplicationBuilderExtensionsTests
    {
        [Test]
        public void UseGrpcWeb_WithoutServices_RaiseError()
        {
            // Arrange
            var services = new ServiceCollection();
            var app = new ApplicationBuilder(services.BuildServiceProvider());

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => app.UseGrpcWeb());
            Assert.AreEqual("Unable to find the required services. Please add all the required services by calling " +
                    "'IServiceCollection.AddGrpcWeb' inside the call to 'ConfigureServices(...)' in the application startup code.", ex.Message);
        }

        [Test]
        public void UseGrpcWeb_WithServices_Success()
        {
            // Arrange
            var services = new ServiceCollection();
            services.AddGrpcWeb();
            var app = new ApplicationBuilder(services.BuildServiceProvider());

            // Act & Assert
            app.UseGrpcWeb();
        }
    }
}
