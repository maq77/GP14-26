using Microsoft.EntityFrameworkCore;
using SSSP.DAL.Abstractions;
using SSSP.DAL.Context;
using SSSP.Infrastructure.Persistence.Interfaces;
using System.Linq.Expressions;

namespace SSSP.Infrastructure.Persistence.Repos
{
    public class Repository<T, TKey> : IRepository<T, TKey>
        where T : class, IEntity<TKey>
    {
        protected readonly AppDbContext Db;
        protected readonly DbSet<T> Set;

        public Repository(AppDbContext db)
        {
            Db = db ?? throw new ArgumentNullException(nameof(db));
            Set = Db.Set<T>();
        }

        public IQueryable<T> Query => Set;

        public async Task<T?> GetByIdAsync(TKey id, CancellationToken ct = default)
        {
            return await Set.FindAsync(new object[] { id }, ct);
        }

        public async Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default)
        {
            return await Set
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task<IEnumerable<T>> GetWhereAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default)
        {
            return await Set
                .AsNoTracking()
                .Where(predicate)
                .ToListAsync(ct);
        }

        public async Task<T?> FirstOrDefaultAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default)
        {
            return await Set
                .AsNoTracking()
                .FirstOrDefaultAsync(predicate, ct);
        }

        public async Task<bool> AnyAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default)
        {
            return await Set.AnyAsync(predicate, ct);
        }

        public async Task<T> AddAsync(T entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            await Set.AddAsync(entity, ct);
            return entity;
        }

        public Task UpdateAsync(T entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            Set.Update(entity);
            return Task.CompletedTask;
        }

        public async Task DeleteAsync(TKey id, CancellationToken ct = default)
        {
            var entity = await GetByIdAsync(id, ct);
            if (entity is null)
                return;

            Set.Remove(entity);
        }
    }
}
