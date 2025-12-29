using Microsoft.EntityFrameworkCore.Storage;
using SSSP.DAL.Abstractions;

namespace SSSP.Infrastructure.Persistence.Interfaces
{
    public interface IUnitOfWork : IDisposable, IAsyncDisposable
    {
        IRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
            where TEntity : class, IEntity<TKey>;

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        Task BeginTransactionAsync(CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);

    }
}
