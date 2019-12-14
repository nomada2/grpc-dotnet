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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client.Web.Internal;
using NUnit.Framework;

namespace Grpc.AspNetCore.FunctionalTests.Web.Client
{
    [TestFixture]
    public class Base64ResponseStreamTests
    {
        [Test]
        public void DecodeBase64DataFragments_MultipleFragments_Success()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("AAAAAAYKBHRlc3Q=gAAAABANCmdycGMtc3RhdHVzOiAw");

            // Act
            var bytesWritten = Base64ResponseStream.DecodeBase64DataFragments(data);

            // Assert
            Assert.AreEqual(32, bytesWritten);

            var expected = Convert.FromBase64String("AAAAAAYKBHRlc3Q=")
                .Concat(Convert.FromBase64String("gAAAABANCmdycGMtc3RhdHVzOiAw"))
                .ToArray();
            var resolvedData = data.AsSpan(0, bytesWritten).ToArray();

            CollectionAssert.AreEqual(expected, resolvedData);
        }

        [Test]
        public async Task ReadAsync_MultipleReads_SmallDataSingleRead_Success()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("AAAAAAYKBHRlc3Q=gAAAABANCmdycGMtc3RhdHVzOiAw");

            var ms = new MemoryStream(data);
            var gprcWebStream = new Base64ResponseStream(ms);

            // Act 1
            var messageHeadData = await ReadContent(gprcWebStream, 5);

            // Assert 1
            Assert.AreEqual(0, messageHeadData[0]);
            Assert.AreEqual(0, messageHeadData[1]);
            Assert.AreEqual(0, messageHeadData[2]);
            Assert.AreEqual(0, messageHeadData[3]);
            Assert.AreEqual(6, messageHeadData[4]);

            // Act 2
            var messageData = await ReadContent(gprcWebStream, 6);

            // Assert 2
            var s = Encoding.UTF8.GetString(messageData.AsSpan(2));
            Assert.AreEqual("test", s);

            // Act 3
            var footerHeadData = await ReadContent(gprcWebStream, 5);

            // Assert 3
            Assert.AreEqual(128, footerHeadData[0]);
            Assert.AreEqual(0, footerHeadData[1]);
            Assert.AreEqual(0, footerHeadData[2]);
            Assert.AreEqual(0, footerHeadData[3]);
            Assert.AreEqual(16, footerHeadData[4]);

            // Act 3
            StreamReader r = new StreamReader(gprcWebStream, Encoding.UTF8);
            var footerText = await r.ReadToEndAsync();

            Assert.AreEqual("\r\ngrpc-status: 0", footerText);
        }

        private static async Task<byte[]> ReadContent(Stream responseStream, uint length, CancellationToken cancellationToken = default)
        {
            // Read message content until content length is reached
            byte[] messageData;
            if (length > 0)
            {
                var received = 0;
                var read = 0;
                messageData = new byte[length];
                while ((read = await responseStream.ReadAsync(messageData.AsMemory(received, messageData.Length - received), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    received += read;

                    if (received == messageData.Length)
                    {
                        break;
                    }
                }
            }
            else
            {
                messageData = Array.Empty<byte>();
            }

            return messageData;
        }

        [Test]
        public async Task ReadAsync_SmallDataSingleRead_Success()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Hello world");

            var ms = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(data)));
            var gprcWebStream = new Base64ResponseStream(ms);

            // Act
            var buffer = new byte[1024];
            var read = await gprcWebStream.ReadAsync(buffer);

            // Assert
            Assert.AreEqual(read, data.Length);
            CollectionAssert.AreEqual(data, data.AsSpan(0, read).ToArray());
        }

        [Test]
        public async Task ReadAsync_SingleByteReads_Success()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Hello world");

            var ms = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(data)));
            var gprcWebStream = new Base64ResponseStream(ms);

            // Act
            var allData = new List<byte>();
            var buffer = new byte[1];

            int read;
            while ((read = await gprcWebStream.ReadAsync(buffer)) > 0)
            {
                allData.AddRange(buffer.AsSpan(0, read).ToArray());
            }
            var readData = allData.ToArray();

            // Assert
            CollectionAssert.AreEqual(data, readData);
        }

        [Test]
        public async Task ReadAsync_TwoByteReads_Success()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("Hello world");

            var ms = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(data)));
            var gprcWebStream = new Base64ResponseStream(ms);

            // Act
            var allData = new List<byte>();
            var buffer = new byte[2];

            int read;
            while ((read = await gprcWebStream.ReadAsync(buffer)) > 0)
            {
                allData.AddRange(buffer.AsSpan(0, read).ToArray());
            }
            var readData = allData.ToArray();

            // Assert
            CollectionAssert.AreEqual(data, readData);
        }

        [TestCase("Hello world", 1)]
        [TestCase("Hello world", 2)]
        [TestCase("Hello world", 3)]
        [TestCase("Hello world", 4)]
        [TestCase("Hello world", 5)]
        [TestCase("Hello world", 6)]
        [TestCase("Hello world", 10)]
        [TestCase("Hello world", 100)]
        [TestCase("The quick brown fox jumped over the lazy dog", 12)]
        public async Task ReadAsync_sdfsdf_Success(string message, int readSize)
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes(message);

            var ms = new MemoryStream(Encoding.UTF8.GetBytes(Convert.ToBase64String(data)));
            var gprcWebStream = new Base64ResponseStream(ms);

            // Act
            var allData = new List<byte>();
            var buffer = new byte[readSize];

            int read;
            while ((read = await gprcWebStream.ReadAsync(buffer)) > 0)
            {
                allData.AddRange(buffer.AsSpan(0, read).ToArray());
            }
            var readData = allData.ToArray();

            // Assert
            CollectionAssert.AreEqual(data, readData);
        }
    }
}
