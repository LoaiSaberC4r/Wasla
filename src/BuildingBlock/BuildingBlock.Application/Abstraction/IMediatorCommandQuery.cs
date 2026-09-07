using BuildingBlock.Domain.Results;
using MediatR;

namespace BuildingBlock.Application.Abstraction
{
    public interface ICommand<TResponse> : IRequest<Result<TResponse>>
    { }

    public interface ICommand : IRequest<Result>
    { }

    public interface IQuery<TResponse> : IRequest<Result<TResponse>>
    { }

    public interface ICommandHandler<TCommand, TResponse> : IRequestHandler<TCommand, Result<TResponse>>
        where TCommand : ICommand<TResponse>
    { }

    public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
        where TCommand : ICommand
    { }

    public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
        where TQuery : IQuery<TResponse>
    { }
}
