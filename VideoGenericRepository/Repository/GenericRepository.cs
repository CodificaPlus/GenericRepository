using Microsoft.EntityFrameworkCore;
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

        public void Dispose() => _context.Dispose();
    }
}
