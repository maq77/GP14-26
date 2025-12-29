using Microsoft.EntityFrameworkCore.Storage;
using SSSP.DAL.Context;
using SSSP.DAL.Abstractions;
using SSSP.Infrastructure.Persistence.Interfaces;
using SSSP.Infrastructure.Persistence.Repos;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace SSSP.Infrastructure.Persistence.UnitOfWork
{
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _db;
        private IDbContextTransaction? _tx;
        private bool _disposed;

        private readonly ConcurrentDictionary<(Type Entity, Type Key), object> _repositories
            = new();

        public UnitOfWork(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));

        }

        public IRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
            where TEntity : class, IEntity<TKey>
        {
            var key = (typeof(TEntity), typeof(TKey));

            if (_repositories.TryGetValue(key, out var repo))
                return (IRepository<TEntity, TKey>)repo;

            var newRepo = new Repository<TEntity, TKey>(_db);
            _repositories[key] = newRepo;

            return newRepo;
        }

        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_tx != null) return;

            _tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_tx == null)
            {
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            await _db.SaveChangesAsync(cancellationToken);
            await _tx.CommitAsync(cancellationToken);
            await _tx.DisposeAsync();
            _tx = null;
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_tx == null) return;

            await _tx.RollbackAsync(cancellationToken);
            await _tx.DisposeAsync();
            _tx = null;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
            => _db.SaveChangesAsync(cancellationToken);

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            if (_tx != null)
            {
                await _tx.DisposeAsync();
                _tx = null;
            }

            await _db.DisposeAsync();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _tx?.Dispose();
            _db.Dispose();
        }
    }
}
