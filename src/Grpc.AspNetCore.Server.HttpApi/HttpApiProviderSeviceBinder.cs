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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Google.Api;
using Google.Protobuf.Reflection;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Grpc.AspNetCore.Server.HttpApi
{
    internal class HttpApiProviderServiceBinder<TService> : ServiceBinderBase where TService : class
    {
        // Protobuf id of the HttpRule field
        private const int HttpRuleFieldId = 72295728;

        private readonly ServiceMethodProviderContext<TService> _context;
        private readonly Type _declaringType;
        private readonly ServiceDescriptor _serviceDescriptor;
        private readonly GrpcServiceOptions _globalOptions;
        private readonly GrpcServiceOptions<TService> _serviceOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;

        internal HttpApiProviderServiceBinder(
            ServiceMethodProviderContext<TService> context,
            Type declaringType,
            ServiceDescriptor serviceDescriptor,
            GrpcServiceOptions globalOptions,
            GrpcServiceOptions<TService> serviceOptions,
            IServiceProvider serviceProvider,
            ILoggerFactory loggerFactory)
        {
            _context = context;
            _declaringType = declaringType;
            _serviceDescriptor = serviceDescriptor;
            _globalOptions = globalOptions;
            _serviceOptions = serviceOptions;
            _serviceProvider = serviceProvider;
            _logger = loggerFactory.CreateLogger<HttpApiProviderServiceBinder<TService>>();
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ClientStreamingServerMethod<TRequest, TResponse> handler)
        {
            if (TryGetHttpRule(method.Name, out _))
            {
                Log.StreamingMethodNotSupported(_logger, method.Name, typeof(TService));
            }
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, DuplexStreamingServerMethod<TRequest, TResponse> handler)
        {
            if (TryGetHttpRule(method.Name, out _))
            {
                Log.StreamingMethodNotSupported(_logger, method.Name, typeof(TService));
            }
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, ServerStreamingServerMethod<TRequest, TResponse> handler)
        {
            if (TryGetHttpRule(method.Name, out _))
            {
                Log.StreamingMethodNotSupported(_logger, method.Name, typeof(TService));
            }
        }

        public override void AddMethod<TRequest, TResponse>(Method<TRequest, TResponse> method, UnaryServerMethod<TRequest, TResponse> handler)
        {
            if (TryGetHttpRule(method.Name, out var httpRule) &&
                TryResolvePattern(httpRule, out var pattern, out var httpVerb))
            {
                AddMethodCore(method, pattern, httpVerb);
            }
            else
            {
                AddMethodCore(method, method.FullName, "GET");
            }
        }

        private void AddMethodCore<TRequest, TResponse>(Method<TRequest, TResponse> method, string pattern, string httpVerb)
            where TRequest : class
            where TResponse : class
        {
            var (invoker, metadata) = CreateModelCore<UnaryServerMethod<TService, TRequest, TResponse>>(
                method.Name,
                new[] { typeof(TRequest), typeof(ServerCallContext) },
                httpVerb);

            var httpApiMethod = new Method<TRequest, TResponse>(method.Type, pattern, string.Empty, method.RequestMarshaller, method.ResponseMarshaller);
            var methodContext = MethodContext.Create<TRequest, TResponse>(new[] { _serviceOptions, _globalOptions });

            var unaryInvoker = new UnaryMethodInvoker<TService, TRequest, TResponse>(httpApiMethod, invoker, methodContext, _serviceProvider);
            var unaryServerCallHandler = new UnaryServerCallHandler<TService, TRequest, TResponse>(unaryInvoker);

            _context.AddUnaryMethod<TRequest, TResponse>(httpApiMethod, metadata, unaryServerCallHandler.HandleCallAsync);
        }

        private bool TryGetHttpRule(string methodName, [NotNullWhen(true)]out HttpRule? httpRule)
        {
            var methodDescriptor = _serviceDescriptor.Methods.SingleOrDefault(m => m.Name == methodName);
            if (methodDescriptor != null)
            {
                return methodDescriptor.CustomOptions.TryGetMessage<HttpRule>(HttpRuleFieldId, out httpRule);
            }

            httpRule = null;
            return false;
        }

        private bool TryResolvePattern(HttpRule http, [NotNullWhen(true)]out string? pattern, [NotNullWhen(true)]out string? verb)
        {
            switch (http.PatternCase)
            {
                case HttpRule.PatternOneofCase.Get:
                    pattern = http.Get;
                    verb = "GET";
                    return true;
                case HttpRule.PatternOneofCase.Put:
                    pattern = http.Put;
                    verb = "PUT";
                    return true;
                case HttpRule.PatternOneofCase.Post:
                    pattern = http.Post;
                    verb = "POST";
                    return true;
                case HttpRule.PatternOneofCase.Delete:
                    pattern = http.Delete;
                    verb = "DELETE";
                    return true;
                case HttpRule.PatternOneofCase.Patch:
                    pattern = http.Patch;
                    verb = "PATCH";
                    return true;
                case HttpRule.PatternOneofCase.Custom:
                    pattern = http.Custom.Path;
                    verb = http.Custom.Kind;
                    return true;
                default:
                    pattern = null;
                    verb = null;
                    return false;
            }
        }

        private (TDelegate invoker, List<object> metadata) CreateModelCore<TDelegate>(string methodName, Type[] methodParameters, string verb) where TDelegate : Delegate
        {
            var handlerMethod = GetMethod(methodName, methodParameters);

            if (handlerMethod == null)
            {
                throw new InvalidOperationException($"Could not find '{methodName}' on {typeof(TService)}.");
            }

            var invoker = (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), handlerMethod);

            var metadata = new List<object>();
            // Add type metadata first so it has a lower priority
            metadata.AddRange(typeof(TService).GetCustomAttributes(inherit: true));
            // Add method metadata last so it has a higher priority
            metadata.AddRange(handlerMethod.GetCustomAttributes(inherit: true));
            metadata.Add(new HttpMethodMetadata(new[] { verb }));

            return (invoker, metadata);
        }

        private MethodInfo? GetMethod(string methodName, Type[] methodParameters)
        {
            Type? currentType = typeof(TService);
            while (currentType != null)
            {
                var matchingMethod = currentType.GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.Instance,
                    binder: null,
                    types: methodParameters,
                    modifiers: null);

                if (matchingMethod == null)
                {
                    return null;
                }

                // Validate that the method overrides the virtual method on the base service type.
                // If there is a method with the same name it will hide the base method. Ignore it,
                // and continue searching on the base type.
                if (matchingMethod.IsVirtual)
                {
                    var baseDefinitionMethod = matchingMethod.GetBaseDefinition();
                    if (baseDefinitionMethod != null && baseDefinitionMethod.DeclaringType == _declaringType)
                    {
                        return matchingMethod;
                    }
                }

                currentType = currentType.BaseType;
            }

            return null;
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Type, Exception?> _streamingMethodNotSupported =
                LoggerMessage.Define<string, Type>(LogLevel.Warning, new EventId(1, "StreamingMethodNotSupported"), "Unable to bind {MethodName} on {ServiceType} to HTTP API. Streaming methods are not supported.");

            public static void StreamingMethodNotSupported(ILogger logger, string methodName, Type serviceType)
            {
                _streamingMethodNotSupported(logger, methodName, serviceType, null);
            }
        }
    }
}
