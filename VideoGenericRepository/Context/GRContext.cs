using Microsoft.EntityFrameworkCore;
using VideoGenericRepository.Models;

namespace VideoGenericRepository.Context
{
    public class GRContext : DbContext
    {
        public DbSet<Produto> Produtos => Set<Produto>();
        public GRContext(DbContextOptions<GRContext> options) : base(options)
        {
        }
    }
}
