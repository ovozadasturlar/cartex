namespace Cartex.Application.Common.Messaging;

public interface ICommand<out TResponse> : IRequest<TResponse>;
