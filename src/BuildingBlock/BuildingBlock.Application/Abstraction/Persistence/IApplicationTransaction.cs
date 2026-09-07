namespace BuildingBlock.Application.Abstraction.Persistence
{
    public interface IApplicationTransaction : IAsyncDisposable
    {
        Task CommitAsync(CancellationToken ct = default);

        Task RollbackAsync(CancellationToken ct = default);
    }
}
