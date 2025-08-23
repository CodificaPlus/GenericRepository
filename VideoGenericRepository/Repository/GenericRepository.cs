using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Linq.Expressions;
using VideoGenericRepository.Context;

namespace VideoGenericRepository.Repository
{
    public abstract class GenericRepository<T> : IDisposable where T : class, new()
    {
        protected readonly GRContext _context;
        protected readonly DbSet<T> _dbSet;
        protected GenericRepository(GRContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<T>();
        }

        #region Metodos de Leitura

        public Task<List<T>> QueryAsync(
         Func<IQueryable<T>, IQueryable<T>> build,
            bool asNoTracking = true, CancellationToken ct = default)
            => build(Query(asNoTracking)).ToListAsync(ct);

        public virtual Task<List<T>> FindAsync(
            Expression<Func<T, bool>> predicate,
            bool asNoTracking = true,
            CancellationToken ct = default)
            => Query(asNoTracking).Where(predicate).ToListAsync(ct);

        public virtual ValueTask<T?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => _dbSet.FindAsync([id], ct);

        public virtual Task<List<T>> FindAllAsync(
            bool asNoTracking = true,
            CancellationToken ct = default)
            => Query(asNoTracking).ToListAsync(ct);

        public virtual Task<bool> ExistsAsync(
            Expression<Func<T, bool>> predicate,
            CancellationToken ct = default)
            => _dbSet.AnyAsync(predicate, ct);

        public virtual Task<int> CountAsync(
            Expression<Func<T, bool>>? predicate = null,
            CancellationToken ct = default)
            => predicate is null ? _dbSet.CountAsync(ct) : _dbSet.CountAsync(predicate, ct);
        #endregion

        #region Paginação e Ordenação
        public virtual async Task<(List<T> Items, int Total)> FindPagedAsync(
            int page, int pageSize,
            Expression<Func<T, bool>>? predicate = null,
            Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
            bool asNoTracking = true,
            CancellationToken ct = default)
        {
            if (page <= 0 || pageSize <= 0) throw new ArgumentOutOfRangeException();

            var query = Query(asNoTracking);
            if (predicate is not null) query = query.Where(predicate);
            var total = await query.CountAsync(ct);

            if (orderBy is not null) query = orderBy(query);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

            return (items, total);
        }
        #endregion

        #region Metodos de Escrita
        public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            await _dbSet.AddAsync(entity, ct);
            await SaveChangesAsync(ct);
        }

        public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(entities);
            await _dbSet.AddRangeAsync(entities, ct);
            await SaveChangesAsync(ct);
        }

        public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            _dbSet.Update(entity);
            await SaveChangesAsync(ct);
        }

        public virtual async Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(entities);
            _dbSet.UpdateRange(entities);
            await SaveChangesAsync(ct);
        }

        public virtual async Task DeleteAsync(T entity, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(entity);
            _dbSet.Remove(entity);
            await SaveChangesAsync(ct);
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _context.SaveChangesAsync(ct);
        #endregion

        #region Transações
        public async Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)
        {
            if (_context.Database.CurrentTransaction is not null)
            {
                await action();
                return;
            }

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                await action();
                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public bool HasActiveTransaction()
            => _context.Database.CurrentTransaction is not null;

        public void Dispose() => _context.Dispose();
        #endregion


        #region Metodos Privados
        private IQueryable<T> Query(bool asNoTracking = true)
         => asNoTracking ? _dbSet.AsNoTracking() : _dbSet;
        #endregion
    }
}
