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
using System.Net.Http.Headers;

namespace Grpc.Net.Client.Web.Internal
{
    internal static class GrpcWebProtocolConstants
    {
        public static MediaTypeHeaderValue GrpcWebTextHeader = new MediaTypeHeaderValue("application/grpc-web-text");
        public static MediaTypeHeaderValue GrpcWebHeader = new MediaTypeHeaderValue("application/grpc-web");
        public static MediaTypeHeaderValue GrpcHeader = new MediaTypeHeaderValue("application/grpc");
        public static Version Http20 = new Version(2, 0);
    }
}
