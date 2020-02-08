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
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client.Web.Internal;
using NUnit.Framework;

namespace Grpc.Net.Client.Tests.Web
{
    [TestFixture]
    public class Base64ResponseStreamTests
    {
        [Test]
        public async Task ReadAsync_ReadLargeData_Success()
        {
            // Arrange
            var headerData = new byte[] { 0, 0, 1, 0, 4 };
            var length = 65540;
            var content = CreateTestData(length);

            var messageContent = Encoding.UTF8.GetBytes(Convert.ToBase64String(headerData.Concat(content).ToArray()));
            var messageCount = 3;

            var streamContent = new List<byte>();
            for (int i = 0; i < messageCount; i++)
            {
                streamContent.AddRange(messageContent);
            }

            var ms = new LimitedReadMemoryStream(streamContent.ToArray(), 3);
            var base64Stream = new Base64ResponseStream(ms);

            for (int i = 0; i < messageCount; i++)
            {
                // Assert 1
                var resolvedHeaderData = await ReadContent(base64Stream, 5, CancellationToken.None);
                // Act 1
                CollectionAssert.AreEqual(headerData, resolvedHeaderData);

                // Assert 2
                var resolvedContentData = await ReadContent(base64Stream, (uint)length, CancellationToken.None);
                // Act 2
                CollectionAssert.AreEqual(content, resolvedContentData);
            }
        }

        private class SegmentedMemoryStream : Stream
        {
            private readonly byte[][] _segments;
            private readonly int _maxReadLength;

            private int _segmentIndex;
            private MemoryStream _currentInnerStream;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotImplementedException();
            public override long Position
            {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }

            public SegmentedMemoryStream(byte[][] segments, int maxReadLength)
            {
                _segments = segments;
                _maxReadLength = maxReadLength;
                _currentInnerStream = CreateInnerStream();
            }

            public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
            {
                var resolvedDestination = destination.Slice(0, Math.Min(_maxReadLength, destination.Length));
                return ReadAsyncCore(resolvedDestination);
            }

            private async ValueTask<int> ReadAsyncCore(Memory<byte> destination)
            {
                do
                {
                    var count = await _currentInnerStream.ReadAsync(destination);
                    if (count > 0)
                    {
                        return count;
                    }

                    _segmentIndex++;
                    if (_segmentIndex >= _segments.Length)
                    {
                        return 0;
                    }

                    _currentInnerStream = CreateInnerStream();
                }
                while (true);
            }

            private MemoryStream CreateInnerStream()
            {
                try
                {
                    return new MemoryStream(_segments[_segmentIndex]);
                }
                catch (Exception ex)
                {

                    throw ex;
                }
            }

            public override void Flush()
            {
                throw new NotImplementedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotImplementedException();
            }

            public override void SetLength(long value)
            {
                throw new NotImplementedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }
        }

        private class LimitedReadMemoryStream : MemoryStream
        {
            private readonly int _maxReadLength;

            public LimitedReadMemoryStream(byte[] buffer, int maxReadLength) : base(buffer)
            {
                _maxReadLength = maxReadLength;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
            {
                var resolvedDestination = destination.Slice(0, Math.Min(_maxReadLength, destination.Length));
                return base.ReadAsync(resolvedDestination, cancellationToken);
            }
        }

        private static byte[] CreateTestData(int size)
        {
            var data = new byte[size];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)i; // Will loop around back to zero
            }
            return data;
        }

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
        public async Task ReadAsync_Randomizer()
        {
            for (int i = 0; i < 100000; i++)
            {
                var segments = Base64RequestStreamTests.BuildSegments();
                var base64Segments = new byte[segments.Length][];
                for (var j = 0; j < segments.Length; j++)
                {
                    base64Segments[j] = Encoding.UTF8.GetBytes(Convert.ToBase64String(segments[j]));
                }

                await NewMethod(i + 1, base64Segments, segments);
            }
        }

        public async Task NewMethod(int bufferSize, byte[][] base64Segments, byte[][] segments)
        {
            var data = Base64RequestStreamTests.Concat(segments).ToArray();

            // Arrange
            var ms = new SegmentedMemoryStream(base64Segments, 100000);
            var base64Stream = new Base64ResponseStream(ms);

            var random = new Random();

            var outputMs = new MemoryStream();
            while (true)
            {
                byte[] buffer = new byte[random.Next(1, bufferSize)];
                var count = await base64Stream.ReadAsync(buffer);
                if (count == 0)
                {
                    break;
                }
                outputMs.Write(buffer.AsSpan(0, count));
            }

            CollectionAssert.AreEqual(data, outputMs.ToArray());
        }

        [Test]
        public async Task ReadAsync_MultipleReads_SmallDataSingleRead_Success()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes("AAAAAAYKBHRlc3Q=gAAAABANCmdycGMtc3RhdHVzOiAw");

            var ms = new LimitedReadMemoryStream(data, 3);
            var base64Stream = new Base64ResponseStream(ms);

            // Act 1
            var messageHeadData = await ReadContent(base64Stream, 5);

            // Assert 1
            Assert.AreEqual(0, messageHeadData[0]);
            Assert.AreEqual(0, messageHeadData[1]);
            Assert.AreEqual(0, messageHeadData[2]);
            Assert.AreEqual(0, messageHeadData[3]);
            Assert.AreEqual(6, messageHeadData[4]);

            // Act 2
            var messageData = await ReadContent(base64Stream, 6);

            // Assert 2
            var s = Encoding.UTF8.GetString(messageData.AsSpan(2));
            Assert.AreEqual("test", s);

            // Act 3
            var footerHeadData = await ReadContent(base64Stream, 5);

            // Assert 3
            Assert.AreEqual(128, footerHeadData[0]);
            Assert.AreEqual(0, footerHeadData[1]);
            Assert.AreEqual(0, footerHeadData[2]);
            Assert.AreEqual(0, footerHeadData[3]);
            Assert.AreEqual(16, footerHeadData[4]);

            // Act 3
            StreamReader r = new StreamReader(base64Stream, Encoding.UTF8);
            var footerText = await r.ReadToEndAsync();

            Assert.AreEqual("\r\ngrpc-status: 0", footerText);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        public async Task ReadAsync_MultipleReadsWithLimitedData_Success(int readSize)
        {
            // Arrange
            var base64Data = Encoding.UTF8.GetBytes("AAAAAAYKBHRlc3Q=gAAAABANCmdycGMtc3RhdHVzOiAw");

            var ms = new LimitedReadMemoryStream(base64Data, readSize);
            var base64Stream = new Base64ResponseStream(ms);

            // Act 1
            var messageHeadData = await ReadContent(base64Stream, 5);

            // Assert 1
            Assert.AreEqual(0, messageHeadData[0]);
            Assert.AreEqual(0, messageHeadData[1]);
            Assert.AreEqual(0, messageHeadData[2]);
            Assert.AreEqual(0, messageHeadData[3]);
            Assert.AreEqual(6, messageHeadData[4]);

            // Act 2
            var messageData = await ReadContent(base64Stream, 6);

            // Assert 2
            var s = Encoding.UTF8.GetString(messageData.AsSpan(2));
            Assert.AreEqual("test", s);

            // Act 3
            var footerHeadData = await ReadContent(base64Stream, 5);

            // Assert 3
            Assert.AreEqual(128, footerHeadData[0]);
            Assert.AreEqual(0, footerHeadData[1]);
            Assert.AreEqual(0, footerHeadData[2]);
            Assert.AreEqual(0, footerHeadData[3]);
            Assert.AreEqual(16, footerHeadData[4]);

            // Act 3
            var footerContentData = await ReadContent(base64Stream, 16);

            var expected = Convert.FromBase64String("AAAAAAYKBHRlc3Q=")
               .Concat(Convert.FromBase64String("gAAAABANCmdycGMtc3RhdHVzOiAw"))
               .ToArray();
            var actual = messageHeadData
                .Concat(messageData)
                .Concat(footerHeadData)
                .Concat(footerContentData)
                .ToArray();

            Assert.AreEqual(expected, actual);
        }

        private static async Task<byte[]> ReadContent(Stream responseStream, uint length, CancellationToken cancellationToken = default)
        {
            // Read message content until content length is reached
            byte[] messageData;
            if (length > 0)
            {
                var received = 0;
                int read;
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
            var base64Stream = new Base64ResponseStream(ms);

            // Act
            var buffer = new byte[1024];
            var read = await base64Stream.ReadAsync(buffer);

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
            var base64Stream = new Base64ResponseStream(ms);

            // Act
            var allData = new List<byte>();
            var buffer = new byte[1];

            int read;
            while ((read = await base64Stream.ReadAsync(buffer)) > 0)
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
            var base64Stream = new Base64ResponseStream(ms);

            // Act
            var allData = new List<byte>();
            var buffer = new byte[2];

            int read;
            while ((read = await base64Stream.ReadAsync(buffer)) > 0)
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
            var base64Stream = new Base64ResponseStream(ms);

            // Act
            var allData = new List<byte>();
            var buffer = new byte[readSize];

            int read;
            while ((read = await base64Stream.ReadAsync(buffer)) > 0)
            {
                allData.AddRange(buffer.AsSpan(0, read).ToArray());
            }
            var readData = allData.ToArray();

            // Assert
            CollectionAssert.AreEqual(data, readData);
        }
    }
}
