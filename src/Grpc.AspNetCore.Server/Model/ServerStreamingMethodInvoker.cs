using System;
using System.Threading.Tasks;
using Grpc.AspNetCore.Server.Internal;
using Grpc.AspNetCore.Server.Internal.CallHandlers;
using Grpc.AspNetCore.Server.Model;
using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace Grpc.AspNetCore.Server.Model
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public class ServerStreamingMethodInvoker<TService, TRequest, TResponse> : MethodInvokerBase<TService, TRequest, TResponse>
        where TRequest : class
        where TResponse : class
        where TService : class
    {
        private readonly ServerStreamingServerMethod<TService, TRequest, TResponse> _invoker;
        private readonly ServerStreamingServerMethod<TRequest, TResponse>? _pipelineInvoker;

        public ServerStreamingMethodInvoker(
            Method<TRequest, TResponse> method,
            ServerStreamingServerMethod<TService, TRequest, TResponse> invoker,
            MethodContext methodContext,
            IServiceProvider serviceProvider)
            : base(method, methodContext, serviceProvider)
        {
            _invoker = invoker;

            if (MethodContext.HasInterceptors)
            {
                var interceptorPipeline = new InterceptorPipelineBuilder<TRequest, TResponse>(MethodContext.Interceptors, ServiceProvider);
                _pipelineInvoker = interceptorPipeline.ServerStreamingPipeline(ResolvedInterceptorInvoker);
            }
        }

        private async Task ResolvedInterceptorInvoker(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext resolvedContext)
        {
            GrpcActivatorHandle<TService> serviceHandle = default;
            try
            {
                serviceHandle = ServiceActivator.Create(resolvedContext.GetHttpContext().RequestServices);
                await _invoker(
                    serviceHandle.Instance,
                    request,
                    responseStream,
                    resolvedContext);
            }
            finally
            {
                if (serviceHandle.Instance != null)
                {
                    await ServiceActivator.ReleaseAsync(serviceHandle);
                }
            }
        }

        public async Task Invoke(HttpContext httpContext, ServerCallContext serverCallContext, TRequest request, IServerStreamWriter<TResponse> streamWriter)
        {
            if (_pipelineInvoker == null)
            {
                GrpcActivatorHandle<TService> serviceHandle = default;
                try
                {
                    serviceHandle = ServiceActivator.Create(httpContext.RequestServices);
                    await _invoker(
                        serviceHandle.Instance,
                        request,
                        streamWriter,
                        serverCallContext);
                }
                finally
                {
                    if (serviceHandle.Instance != null)
                    {
                        await ServiceActivator.ReleaseAsync(serviceHandle);
                    }
                }
            }
            else
            {
                await _pipelineInvoker(
                    request,
                    streamWriter,
                    serverCallContext);
            }
        }
    }
}
