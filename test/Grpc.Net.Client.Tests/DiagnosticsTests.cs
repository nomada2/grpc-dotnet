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
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Greet;
using Grpc.Core;
using Grpc.Net.Client.Internal;
using Grpc.Net.Client.Tests.Infrastructure;
using Grpc.Tests.Shared;
using NUnit.Framework;

namespace Grpc.Net.Client.Tests
{
    [TestFixture]
    public class DiagnosticsTests
    {
        [Test]
        public async Task Dispose_StartCallInTask_ActivityPreserved()
        {
            // Arrange
            var httpClient = TestHelpers.CreateTestClient(async request =>
            {
                var streamContent = await TestHelpers.CreateResponseContent(new HelloReply()).DefaultTimeout();
                var response = ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent, grpcStatusCode: StatusCode.Aborted);
                response.TrailingHeaders.Add(GrpcProtocolConstants.MessageTrailer, "value");
                return response;
            });
            var invoker = HttpClientCallInvokerFactory.Create(httpClient);

            // Act & Assert
            var a = new Activity("a").Start();
            Assert.AreEqual("a", Activity.Current.OperationName);

            var call = await Task.Run(() =>
            {
                var c = invoker.AsyncDuplexStreamingCall<HelloRequest, HelloReply>(TestHelpers.ServiceMethod, string.Empty, new CallOptions());
                Assert.AreEqual("a", Activity.Current.OperationName);

                return c;
            });
            Assert.AreEqual("a", Activity.Current.OperationName);

            var b = new Activity("b").Start();
            Assert.AreEqual("b", Activity.Current.OperationName);

            call.Dispose();
            Assert.AreEqual("b", Activity.Current.OperationName);
        }

        [Test]
        public void DiagnosticListener_MakeCall_ActivityWritten()
        {
            // Arrange
            HttpRequestMessage? requestMessage = null;
            HttpResponseMessage? responseMessage = null;
            var httpClient = TestHelpers.CreateTestClient(async request =>
            {
                requestMessage = request;

                var streamContent = await TestHelpers.CreateResponseContent(new HelloReply()).DefaultTimeout();
                responseMessage = ResponseUtils.CreateResponse(HttpStatusCode.OK, streamContent, grpcStatusCode: StatusCode.Aborted);
                responseMessage.TrailingHeaders.Add(GrpcProtocolConstants.MessageTrailer, "value");
                return responseMessage;
            });
            var invoker = HttpClientCallInvokerFactory.Create(httpClient);

            var result = new List<KeyValuePair<string, object>>();

            // Act
            using (GrpcDiagnostics.DiagnosticListener.Subscribe(new ObserverToList<KeyValuePair<string, object>>(result)))
            {
                var c = invoker.AsyncDuplexStreamingCall<HelloRequest, HelloReply>(TestHelpers.ServiceMethod, string.Empty, new CallOptions());
                c.Dispose();
            }

            // Assert
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(GrpcDiagnostics.ActivityStartKey, result[0].Key);
            Assert.AreEqual(requestMessage, result[0].Value);
            Assert.AreEqual(GrpcDiagnostics.ActivityStopKey, result[1].Key);
            Assert.AreEqual(responseMessage, result[1].Value);
        }

        internal class ObserverToList<T> : IObserver<T>
        {
            public ObserverToList(List<T> output, Predicate<T>? filter = null, string? name = null)
            {
                _output = output;
                _output.Clear();
                _filter = filter;
                _name = name;
            }

            public bool Completed { get; private set; }

            #region private
            public void OnCompleted()
            {
                Completed = true;
            }

            public void OnError(Exception error)
            {
                Assert.True(false, "Error happened on IObserver");
            }

            public void OnNext(T value)
            {
                Assert.False(Completed);
                if (_filter == null || _filter(value))
                {
                    _output.Add(value);
                }
            }

            private List<T> _output;
            private Predicate<T>? _filter;
            private string? _name;  // for debugging 
            #endregion
        }
    }
}
