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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Net.Client.Web.Internal;
using NUnit.Framework;

namespace Grpc.Net.Client.Tests.Web
{
    [TestFixture]
    public class Base64RequestStreamTests
    {
        [Test]
        public async Task WriteAsync_SmallData_Written()
        {
            // Arrange
            var ms = new MemoryStream();
            var gprcWebStream = new Base64RequestStream(ms);

            var data = Encoding.UTF8.GetBytes("123");

            // Act
            await gprcWebStream.WriteAsync(data);

            // Assert
            var base64 = Encoding.UTF8.GetString(ms.ToArray());
            CollectionAssert.AreEqual(data, Convert.FromBase64String(base64));
        }

        [Test]
        public async Task WriteAsync_Randomizer()
        {
            for (int i = 0; i < 10000; i++)
            {
                var segments = BuildSegments();

                await NewMethod(segments);
            }
        }

        public static byte[][] BuildSegments()
        {
            var random = new Random();

            var count = random.Next(1, 100);
            var segments = new byte[count][];
            for (int i = 0; i < segments.Length; i++)
            {
                var segmentLength = random.Next(1, 100);
                var segment = new byte[segmentLength];
                for (var j = 0; j < segment.Length; j++)
                {
                    segment[j] = (byte)j;
                }

                segments[i] = segment;
            }

            return segments;
        }

        private static async Task NewMethod(byte[][] segments)
        {
            // Arrange
            var ms = new MemoryStream();
            var gprcWebStream = new Base64RequestStream(ms);

            var data = Concat(segments).ToArray();

            // Act
            foreach (var segment in segments)
            {
                await gprcWebStream.WriteAsync(segment);
            }

            await gprcWebStream.FlushAsync(CancellationToken.None);

            // Assert
            var base64 = Encoding.UTF8.GetString(ms.ToArray());
            CollectionAssert.AreEqual(data, Convert.FromBase64String(base64));
        }

        public static T[] Concat<T>(params T[][] arrays)
        {
            // return (from array in arrays from arr in array select arr).ToArray();

            var result = new T[arrays.Sum(a => a.Length)];
            int offset = 0;
            for (int x = 0; x < arrays.Length; x++)
            {
                arrays[x].CopyTo(result, offset);
                offset += arrays[x].Length;
            }
            return result;
        }

        [Test]
        public async Task WriteAsync_MultipleSingleBytes_Written()
        {
            // Arrange
            var ms = new MemoryStream();
            var gprcWebStream = new Base64RequestStream(ms);

            var data = Encoding.UTF8.GetBytes("123");

            // Act
            foreach (var b in data)
            {
                await gprcWebStream.WriteAsync(new[] { b });
            }

            // Assert
            var base64 = Encoding.UTF8.GetString(ms.ToArray());
            CollectionAssert.AreEqual(data, Convert.FromBase64String(base64));
        }

        [Test]
        public async Task WriteAsync_SmallDataWithRemainder_WrittenWithoutRemainder()
        {
            // Arrange
            var ms = new MemoryStream();
            var gprcWebStream = new Base64RequestStream(ms);

            var data = Encoding.UTF8.GetBytes("Hello world");

            // Act
            await gprcWebStream.WriteAsync(data);

            // Assert
            var base64 = Encoding.UTF8.GetString(ms.ToArray());
            var newData = Convert.FromBase64String(base64);
            CollectionAssert.AreEqual(data.AsSpan(0, newData.Length).ToArray(), newData);
        }

        [Test]
        public async Task FlushAsync_HasRemainder_WriteRemainder()
        {
            // Arrange
            var ms = new MemoryStream();
            var gprcWebStream = new Base64RequestStream(ms);

            var data = Encoding.UTF8.GetBytes("Hello world");

            // Act
            await gprcWebStream.WriteAsync(data);
            await gprcWebStream.FlushAsync();

            // Assert
            var base64 = Encoding.UTF8.GetString(ms.ToArray());
            CollectionAssert.AreEqual(data, Convert.FromBase64String(base64));
        }
    }
}
