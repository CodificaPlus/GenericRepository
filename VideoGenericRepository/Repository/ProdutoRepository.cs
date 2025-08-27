using VideoGenericRepository.Context;
using VideoGenericRepository.Models;

namespace VideoGenericRepository.Repository
{
    public class ProdutoRepository : GenericRepository<Produto>
    {
        public ProdutoRepository(GRContext context) : base(context) { }
    }
}
