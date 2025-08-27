using Microsoft.EntityFrameworkCore;
using VideoGenericRepository.Models;

namespace VideoGenericRepository.Context
{
    public class GRContext : DbContext
    {
        public DbSet<Produto> Produto => Set<Produto>();
        public GRContext(DbContextOptions<GRContext> options) : base(options)
        {
        }
    }
}
