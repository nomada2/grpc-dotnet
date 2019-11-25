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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.AspNetCore.Server.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Grpc.AspNetCore.Server.HttpApi
{
    internal class UnaryServerCallHandler<TService, TRequest, TResponse>
        where TService : class
        where TRequest : class
        where TResponse : class
    {
        private readonly UnaryMethodInvoker<TService, TRequest, TResponse> _unaryMethodInvoker;
        private readonly FieldDescriptor? _responseBodyDescriptor;
        private readonly MessageDescriptor? _bodyDescriptor;
        private readonly FieldDescriptor? _bodyFieldDescriptor;
        private readonly List<FieldDescriptor> _routeParameterDescriptors;

        public UnaryServerCallHandler(
            UnaryMethodInvoker<TService, TRequest, TResponse> unaryMethodInvoker,
            FieldDescriptor? responseBodyDescriptor,
            MessageDescriptor? bodyDescriptor,
            FieldDescriptor? bodyFieldDescriptor,
            List<FieldDescriptor> routeParameterDescriptors)
        {
            _unaryMethodInvoker = unaryMethodInvoker;
            _responseBodyDescriptor = responseBodyDescriptor;
            _bodyDescriptor = bodyDescriptor;
            _bodyFieldDescriptor = bodyFieldDescriptor;
            _routeParameterDescriptors = routeParameterDescriptors;
        }

        public async Task HandleCallAsync(HttpContext httpContext)
        {
            var requestMessage = CreateMessage(httpContext);

            foreach (var parameterDescriptor in _routeParameterDescriptors)
            {
                var routeValue = httpContext.Request.RouteValues[parameterDescriptor.Name];
                if (routeValue != null)
                {
                    TryRecursiveSetValue(requestMessage, parameterDescriptor.Name.AsSpan(), routeValue);
                }
            }

            foreach (var item in httpContext.Request.Query)
            {
                object value = item.Value.Count == 1 ? (object)item.Value[0] : item.Value;
                TryRecursiveSetValue(requestMessage, item.Key.AsSpan(), value);
            }

            var serverCallContext = new HttpApiServerCallContext();

            var response = await _unaryMethodInvoker.Invoke(httpContext, serverCallContext, (TRequest)requestMessage);
            object responseBody = response;

            if (_responseBodyDescriptor != null)
            {
                responseBody = _responseBodyDescriptor.Accessor.GetValue((IMessage)responseBody);
            }

            httpContext.Response.StatusCode = StatusCodes.Status200OK;
            httpContext.Response.ContentType = "application/json";

            var ms = new MemoryStream();
            using (var writer = new StreamWriter(ms, leaveOpen: true))
            {
                if (responseBody is IMessage responseMessage)
                {
                    JsonFormatter.Default.Format(responseMessage, writer);
                }
                else
                {
                    JsonFormatter.Default.WriteValue(writer, responseBody);
                }

                writer.Flush();
            }
            ms.Seek(0, SeekOrigin.Begin);

            await ms.CopyToAsync(httpContext.Response.Body);
            await httpContext.Response.Body.FlushAsync();
        }

        private static object ConvertValue(object value, FieldDescriptor descriptor)
        {
            switch (descriptor.FieldType)
            {
                case FieldType.Double:
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
                case FieldType.Float:
                    return Convert.ToSingle(value, CultureInfo.InvariantCulture);
                case FieldType.Int64:
                case FieldType.SInt64:
                case FieldType.SFixed64:
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                case FieldType.UInt64:
                case FieldType.Fixed64:
                    return Convert.ToUInt64(value, CultureInfo.InvariantCulture);
                case FieldType.Int32:
                case FieldType.SInt32:
                case FieldType.SFixed32:
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                case FieldType.Bool:
                    return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                case FieldType.String:
                    return value;
                case FieldType.Bytes:
                    {
                        if (value is string s)
                        {
                            return ByteString.FromBase64(s);
                        }
                        throw new InvalidOperationException("Base64 encoded string required to convert to bytes.");
                    }
                case FieldType.UInt32:
                case FieldType.Fixed32:
                    return Convert.ToUInt32(value, CultureInfo.InvariantCulture);
                case FieldType.Enum:
                    {
                        if (value is string s)
                        {
                            var enumValueDescriptor = descriptor.EnumType.FindValueByName(s);
                            if (enumValueDescriptor == null)
                            {
                                throw new InvalidOperationException($"Invalid enum value '{s}' for enum type {descriptor.EnumType.Name}.");
                            }

                            return enumValueDescriptor.Number;
                        }
                        throw new InvalidOperationException("String required to convert to enum.");
                    }
            }

            throw new InvalidOperationException("Unsupported type: " + descriptor);
        }

        private static bool TryRecursiveSetValue(IMessage currentValue, ReadOnlySpan<char> path, object values)
        {
            var separator = path.IndexOf('.');
            if (separator == -1)
            {
                var field = currentValue.Descriptor.FindFieldByName(path.ToString());
                if (field == null)
                {
                    return false;
                }

                if (field.IsRepeated)
                {
                    var list = (IList)field.Accessor.GetValue(currentValue);
                    if (values is StringValues stringValues)
                    {
                        foreach (var value in stringValues)
                        {
                            list.Add(ConvertValue(value, field));
                        }
                    }
                    else
                    {
                        list.Add(ConvertValue(values, field));
                    }
                }
                else
                {
                    if (values is StringValues stringValues)
                    {
                        if (stringValues.Count == 1)
                        {
                            field.Accessor.SetValue(currentValue, ConvertValue(stringValues[0], field));
                        }
                        else
                        {
                            throw new InvalidOperationException("Can't set multiple values onto a non-repeating field.");
                        }
                    }
                    else
                    {
                        field.Accessor.SetValue(currentValue, ConvertValue(values, field));
                    }
                }

                return true;
            }
            else
            {
                var field = currentValue.Descriptor.FindFieldByName(path.Slice(0, separator).ToString());
                if (field == null)
                {
                    return false;
                }

                var fieldMessage = (IMessage)field.Accessor.GetValue(currentValue);
                var newMessage = (fieldMessage == null);

                if (newMessage)
                {
                    fieldMessage = (IMessage)Activator.CreateInstance(field.MessageType.ClrType)!;
                }

                if (!TryRecursiveSetValue(fieldMessage!, path.Slice(separator + 1), values))
                {
                    return false;
                }

                if (newMessage)
                {
                    field.Accessor.SetValue(currentValue, fieldMessage);
                }

                return true;
            }
        }

        private global::Google.Protobuf.IMessage CreateMessage(HttpContext httpContext)
        {
            IMessage? requestMessage;

            if (_bodyDescriptor != null)
            {
                if (string.Equals(httpContext.Request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Request content-type of application/json is required.");
                }

                var bodyContent = JsonParser.Default.Parse(new StreamReader(httpContext.Request.Body), _bodyDescriptor);
                if (_bodyFieldDescriptor != null)
                {
                    requestMessage = (IMessage)Activator.CreateInstance<TRequest>();
                    _bodyFieldDescriptor.Accessor.SetValue(requestMessage, bodyContent);
                }
                else
                {
                    requestMessage = bodyContent;
                }
            }
            else
            {
                requestMessage = (IMessage)Activator.CreateInstance<TRequest>();
            }

            return requestMessage;
        }
    }
}
