namespace BuildingBlock.Application.Abstraction.Persistence;

/// <summary>
/// Marks a command that requires an explicit application transaction.
/// </summary>
public interface ITransactionalCommand
{
}

/// <summary>
/// Marks a command that requires a transaction owned by the specified write persistence marker.
/// </summary>
/// <typeparam name="TWriteMarker">The write persistence marker that owns the transaction.</typeparam>
public interface ITransactionalCommand<TWriteMarker> : ITransactionalCommand
    where TWriteMarker : IWritePersistenceMarker
{
}
