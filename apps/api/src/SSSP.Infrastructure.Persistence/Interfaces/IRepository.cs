using SSSP.DAL.Abstractions;
using System.Linq.Expressions;

namespace SSSP.Infrastructure.Persistence.Interfaces
{
    public interface IRepository<T, TKey>
        where T : class, IEntity<TKey>
    {
        Task<T?> GetByIdAsync(TKey id, CancellationToken ct = default);
        Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
        Task<IEnumerable<T>> GetWhereAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default);
        Task<T?> FirstOrDefaultAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default);
        Task<bool> AnyAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default);

        Task<T> AddAsync(T entity, CancellationToken ct = default);
        Task UpdateAsync(T entity, CancellationToken ct = default);
        Task DeleteAsync(TKey id, CancellationToken ct = default);
    }
}
