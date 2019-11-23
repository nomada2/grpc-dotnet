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
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.AspNetCore.Server.Model;
using Microsoft.AspNetCore.Http;

namespace Grpc.AspNetCore.Server.HttpApi
{
    internal class UnaryServerCallHandler<TService, TRequest, TResponse>
        where TService : class
        where TRequest : class
        where TResponse : class
    {
        private readonly UnaryMethodInvoker<TService, TRequest, TResponse> _unaryMethodInvoker;

        public UnaryServerCallHandler(UnaryMethodInvoker<TService, TRequest, TResponse> unaryMethodInvoker)
        {
            _unaryMethodInvoker = unaryMethodInvoker;
        }

        public async Task HandleCallAsync(HttpContext httpContext)
        {
            var request = Activator.CreateInstance<TRequest>();
            var requestMessage = (IMessage)request;

            foreach (var item in httpContext.Request.RouteValues)
            {
                var field = requestMessage.Descriptor.FindFieldByName(item.Key);
                field.Accessor.SetValue(requestMessage, item.Value);
            }

            var fields = requestMessage.Descriptor.Fields.InFieldNumberOrder();

            //httpApiMethod.
            //var request = (TRequest)JsonParser.Default.Parse(new StreamReader(context.Request.Body), methodDescriptor.InputType);

            var serverCallContext = new HttpApiServerCallContext();

            var response = await _unaryMethodInvoker.Invoke(httpContext, serverCallContext, request);

            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.ContentType = "application/json";

            var ms = new MemoryStream();
            using (var writer = new StreamWriter(ms, leaveOpen: true))
            {
                JsonFormatter.Default.Format((IMessage)response, writer);
                writer.Flush();
            }
            ms.Seek(0, SeekOrigin.Begin);

            await ms.CopyToAsync(httpContext.Response.Body);
            await httpContext.Response.Body.FlushAsync();
        }
    }
}
