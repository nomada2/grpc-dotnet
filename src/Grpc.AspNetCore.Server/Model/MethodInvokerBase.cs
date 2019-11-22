using System;
using Grpc.AspNetCore.Server.Internal;
using Grpc.Core;

namespace Grpc.AspNetCore.Server.Model
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public abstract class MethodInvokerBase<TService, TRequest, TResponse>
        where TRequest : class
        where TResponse : class
        where TService : class
    {
        public Method<TRequest, TResponse> Method { get; }
        public MethodContext MethodContext { get; }

        private protected IGrpcServiceActivator<TService> ServiceActivator { get; }
        private protected IServiceProvider ServiceProvider { get; }

        protected MethodInvokerBase(
            Method<TRequest, TResponse> method,
            MethodContext methodContext,
            IServiceProvider serviceProvider)
        {
            Method = method;
            MethodContext = methodContext;
            ServiceActivator = new DefaultGrpcServiceActivator<TService>();
            ServiceProvider = serviceProvider;
        }
    }
}
