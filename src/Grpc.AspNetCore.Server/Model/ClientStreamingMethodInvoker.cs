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
    public class ClientStreamingMethodInvoker<TService, TRequest, TResponse> : MethodInvokerBase<TService, TRequest, TResponse>
        where TRequest : class
        where TResponse : class
        where TService : class
    {
        private readonly ClientStreamingServerMethod<TService, TRequest, TResponse> _invoker;
        private readonly ClientStreamingServerMethod<TRequest, TResponse>? _pipelineInvoker;

        public ClientStreamingMethodInvoker(
            Method<TRequest, TResponse> method,
            ClientStreamingServerMethod<TService, TRequest, TResponse> invoker,
            MethodContext methodContext,
            IServiceProvider serviceProvider)
            : base(method, methodContext, serviceProvider)
        {
            _invoker = invoker;

            if (MethodContext.HasInterceptors)
            {
                var interceptorPipeline = new InterceptorPipelineBuilder<TRequest, TResponse>(MethodContext.Interceptors, ServiceProvider);
                _pipelineInvoker = interceptorPipeline.ClientStreamingPipeline(ResolvedInterceptorInvoker);
            }
        }

        private async Task<TResponse> ResolvedInterceptorInvoker(IAsyncStreamReader<TRequest> requestStream, ServerCallContext resolvedContext)
        {
            GrpcActivatorHandle<TService> serviceHandle = default;
            try
            {
                serviceHandle = ServiceActivator.Create(resolvedContext.GetHttpContext().RequestServices);
                return await _invoker(
                    serviceHandle.Instance,
                    requestStream,
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

        public async Task<TResponse> Invoke(HttpContext httpContext, ServerCallContext serverCallContext, IAsyncStreamReader<TRequest> requestStream)
        {
            if (_pipelineInvoker == null)
            {
                GrpcActivatorHandle<TService> serviceHandle = default;
                try
                {
                    serviceHandle = ServiceActivator.Create(httpContext.RequestServices);
                    return await _invoker(
                        serviceHandle.Instance,
                        requestStream,
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
                return await _pipelineInvoker(
                    requestStream,
                    serverCallContext);
            }
        }
    }
}
