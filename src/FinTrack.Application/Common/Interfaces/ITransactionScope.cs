namespace FinTrack.Application.Common.Interfaces;

/// <summary>
/// Abstraction over a database transaction. The unit of work pattern is honored via
/// <see cref="IApplicationDbContext.SaveChangesAsync"/>; the scope only groups it.
/// Disposing without committing rolls back.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
