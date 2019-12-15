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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Net.Client.Internal;
using Grpc.Net.Client.Web.Internal;
using NUnit.Framework;

namespace Grpc.AspNetCore.FunctionalTests.Web.Client
{
    [TestFixture]
    public class GrpcWebResponseStreamTests
    {
        [Test]
        public async Task ReadAsync_EmptyMessage_ParseMessageAndTrailers()
        {
            // Arrange
            var data = Convert.FromBase64String("AAAAAACAAAAAEA0KZ3JwYy1zdGF0dXM6IDA=");
            var httpResponseMessage = new HttpResponseMessage();
            var ms = new MemoryStream(data);
            var responseStream = new GrpcWebResponseStream(ms, httpResponseMessage);

            // Act 1
            var contentHeaderData = new byte[5];
            var read1 = await responseStream.ReadAsync(contentHeaderData);

            // Assert 1
            Assert.AreEqual(5, read1);
            Assert.AreEqual(0, contentHeaderData[0]);
            Assert.AreEqual(0, contentHeaderData[1]);
            Assert.AreEqual(0, contentHeaderData[2]);
            Assert.AreEqual(0, contentHeaderData[3]);
            Assert.AreEqual(0, contentHeaderData[4]);

            // Act 2
            var read2 = await responseStream.ReadAsync(contentHeaderData);

            // Assert 2
            Assert.AreEqual(0, read2);
            Assert.AreEqual(1, httpResponseMessage.TrailingHeaders.Count());
            Assert.AreEqual("0", httpResponseMessage.TrailingHeaders.GetValues(GrpcProtocolConstants.StatusTrailer).Single());
        }

        [Test]
        public async Task ReadAsync_EmptyMessageAndTrailers_ParseMessageAndTrailers()
        {
            // Arrange
            var data = new byte[] { 0, 0, 0, 0, 0, 128, 0, 0, 0, 0 };
            var httpResponseMessage = new HttpResponseMessage();
            var ms = new MemoryStream(data);
            var responseStream = new GrpcWebResponseStream(ms, httpResponseMessage);

            // Act 1
            var contentHeaderData = new byte[5];
            var read1 = await responseStream.ReadAsync(contentHeaderData);

            // Assert 1
            Assert.AreEqual(5, read1);
            Assert.AreEqual(0, contentHeaderData[0]);
            Assert.AreEqual(0, contentHeaderData[1]);
            Assert.AreEqual(0, contentHeaderData[2]);
            Assert.AreEqual(0, contentHeaderData[3]);
            Assert.AreEqual(0, contentHeaderData[4]);

            // Act 2
            var read2 = await responseStream.ReadAsync(contentHeaderData);

            // Assert 2
            Assert.AreEqual(0, read2);
            Assert.AreEqual(0, httpResponseMessage.TrailingHeaders.Count());
        }
    }
}
