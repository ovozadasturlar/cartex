using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace Cartex.Application.Common.Messaging;

public interface IRequest<out TResponse>;

public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);

public interface IPipelineBehavior<in TRequest, TResponse> where TRequest : notnull
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}

public interface INotification;

public interface INotificationHandler<in TNotification> where TNotification : INotification
{
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}

public interface ISender
{
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}

public interface IPublisher
{
    Task Publish(INotification notification, CancellationToken cancellationToken = default);
}

public interface IMediator : ISender, IPublisher;

public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = new();
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);
    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
}

public sealed class Mediator(IServiceProvider provider) : IMediator
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapper> _requestWrappers = new();
    private static readonly ConcurrentDictionary<Type, NotificationHandlerWrapper> _notificationWrappers = new();

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var wrapper = (RequestHandlerWrapper<TResponse>)_requestWrappers.GetOrAdd(request.GetType(), static requestType =>
        {
            var responseType = typeof(TResponse);
            var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
            return (RequestHandlerWrapper)Activator.CreateInstance(wrapperType)!;
        });
        return wrapper.Handle(request, provider, cancellationToken);
    }

    public Task Publish(INotification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        var wrapper = _notificationWrappers.GetOrAdd(notification.GetType(), static notificationType =>
        {
            var wrapperType = typeof(NotificationHandlerWrapperImpl<>).MakeGenericType(notificationType);
            return (NotificationHandlerWrapper)Activator.CreateInstance(wrapperType)!;
        });
        return wrapper.Handle(notification, provider, cancellationToken);
    }

    private abstract class RequestHandlerWrapper;

    private abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapper
    {
        public abstract Task<TResponse> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken);
    }

    private sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> Handle(object request, IServiceProvider provider, CancellationToken cancellationToken)
        {
            var typed = (TRequest)request;
            RequestHandlerDelegate<TResponse> handler = _ =>
                provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>().Handle(typed, cancellationToken);

            foreach (var behavior in provider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse())
            {
                var next = handler;
                handler = _ => behavior.Handle(typed, next, cancellationToken);
            }

            return handler(cancellationToken);
        }
    }

    private abstract class NotificationHandlerWrapper
    {
        public abstract Task Handle(object notification, IServiceProvider provider, CancellationToken cancellationToken);
    }

    private sealed class NotificationHandlerWrapperImpl<TNotification> : NotificationHandlerWrapper
        where TNotification : INotification
    {
        public override async Task Handle(object notification, IServiceProvider provider, CancellationToken cancellationToken)
        {
            var typed = (TNotification)notification;
            foreach (var handler in provider.GetServices<INotificationHandler<TNotification>>())
                await handler.Handle(typed, cancellationToken);
        }
    }
}
