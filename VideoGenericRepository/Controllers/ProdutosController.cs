using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using VideoGenericRepository.Context;
using VideoGenericRepository.Models;
using VideoGenericRepository.Repository;

namespace VideoGenericRepository.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProdutosController : ControllerBase
    {
        private readonly ProdutoRepository _repo;

        public ProdutosController(ProdutoRepository repo)
        {
            _repo = repo;
        }

        [HttpGet]
        public async Task<ActionResult<List<Produto>>> GetAll(
            [FromQuery] bool asNoTracking = true,
            CancellationToken ct = default)
        {
            var itens = await _repo.FindAllAsync(asNoTracking, ct);
            return Ok(itens);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Produto>> GetById(Guid id, CancellationToken ct = default)
        {
            var entity = await _repo.FindByIdAsync(id, ct);
            if (entity is null) return NotFound();
            return Ok(entity);
        }

        [HttpGet("search")]
        public async Task<ActionResult<List<Produto>>> Search(
            [FromQuery] string? nome,
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] bool asNoTracking = true,
            CancellationToken ct = default)
        {
            Expression<Func<Produto, bool>> predicate = p =>
                (string.IsNullOrWhiteSpace(nome) || p.Nome.Contains(nome)) &&
                (!minPrice.HasValue || p.Preco >= minPrice.Value) &&
                (!maxPrice.HasValue || p.Preco <= maxPrice.Value);

            var result = await _repo.FindAsync(predicate, asNoTracking, ct);
            return Ok(result);
        }

        [HttpGet("query")]
        public async Task<ActionResult<List<Produto>>> Query(
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            [FromQuery] string? sort = "nome",
            [FromQuery] string? dir = "asc",
            [FromQuery] bool asNoTracking = true,
            CancellationToken ct = default)
        {
            var items = await _repo.QueryAsync(q =>
            {
                if (minPrice.HasValue) q = q.Where(p => p.Preco >= minPrice.Value);
                if (maxPrice.HasValue) q = q.Where(p => p.Preco <= maxPrice.Value);

                q = (sort?.ToLowerInvariant(), dir?.ToLowerInvariant()) switch
                {
                    ("preco", "desc") => q.OrderByDescending(p => p.Preco),
                    ("preco", _) => q.OrderBy(p => p.Preco),
                    ("nome", "desc") => q.OrderByDescending(p => p.Nome),
                    _ => q.OrderBy(p => p.Nome)
                };

                return q;
            }, asNoTracking, ct);

            return Ok(items);
        }

        [HttpGet("paged")]
        public async Task<ActionResult<object>> Paged(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? nome = null,
            [FromQuery] string? sort = "nome",
            [FromQuery] string dir = "asc",
            [FromQuery] bool asNoTracking = true,
            CancellationToken ct = default)
        {
            Expression<Func<Produto, bool>>? predicate = null;
            if (!string.IsNullOrWhiteSpace(nome))
                predicate = p => p.Nome.Contains(nome);

            Func<IQueryable<Produto>, IOrderedQueryable<Produto>> orderBy = q => (sort?.ToLowerInvariant(), dir.ToLowerInvariant()) switch
            {
                ("preco", "desc") => q.OrderByDescending(p => p.Preco),
                ("preco", _) => q.OrderBy(p => p.Preco),
                ("nome", "desc") => q.OrderByDescending(p => p.Nome),
                _ => q.OrderBy(p => p.Nome)
            };

            var (items, total) = await _repo.FindPagedAsync(page, pageSize, predicate, orderBy, asNoTracking, ct);
            return Ok(new { page, pageSize, total, items });
        }

        [HttpGet("exists")]
        public async Task<ActionResult<bool>> Exists([FromQuery] string nome, CancellationToken ct = default)
        {
            var exists = await _repo.ExistsAsync(p => p.Nome == nome, ct);
            return Ok(exists);
        }

        [HttpGet("count")]
        public async Task<ActionResult<int>> Count(
            [FromQuery] decimal? minPrice,
            [FromQuery] decimal? maxPrice,
            CancellationToken ct = default)
        {
            Expression<Func<Produto, bool>>? predicate = null;
            if (minPrice.HasValue || maxPrice.HasValue)
            {
                predicate = p =>
                    (!minPrice.HasValue || p.Preco >= minPrice.Value) &&
                    (!maxPrice.HasValue || p.Preco <= maxPrice.Value);
            }

            var count = await _repo.CountAsync(predicate, ct);
            return Ok(count);
        }

        [HttpPost]
        public async Task<ActionResult<Produto>> Create([FromBody] Produto model, CancellationToken ct = default)
        {
            if (model.Id == Guid.Empty) model.Id = Guid.NewGuid();
            await _repo.AddAsync(model, ct);
            return CreatedAtAction(nameof(GetById), new { id = model.Id }, model);
        }

        [HttpPost("bulk")]
        public async Task<ActionResult> CreateBulk([FromBody] List<Produto> itens, CancellationToken ct = default)
        {
            foreach (var p in itens)
                if (p.Id == Guid.Empty) p.Id = Guid.NewGuid();

            await _repo.AddRangeAsync(itens, ct);
            return Ok(new { created = itens.Count });
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult> Update(Guid id, [FromBody] Produto model, CancellationToken ct = default)
        {
            if (model.Id != Guid.Empty && model.Id != id)
                return BadRequest("ID do corpo difere do ID da rota.");

            model.Id = id;
            await _repo.UpdateAsync(model, ct);
            return NoContent();
        }

        [HttpPut("bulk")]
        public async Task<ActionResult> UpdateBulk([FromBody] List<Produto> itens, CancellationToken ct = default)
        {
            if (itens.Any(p => p.Id == Guid.Empty))
                return BadRequest("Todos os itens devem possuir Id.");

            await _repo.UpdateRangeAsync(itens, ct);
            return NoContent();
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult> Delete(Guid id, CancellationToken ct = default)
        {
            var stub = new Produto { Id = id };
            await _repo.DeleteAsync(stub, ct);
            return NoContent();
        }

        [HttpPost("tx-demo")]
        public async Task<ActionResult> TransactionDemo([FromQuery] bool fail = false, CancellationToken ct = default)
        {
            await _repo.ExecuteInTransactionAsync(async () =>
            {
                await _repo.AddAsync(new Produto { Id = Guid.NewGuid(), Nome = "Tx A", Preco = 10 }, ct);
                await _repo.AddAsync(new Produto { Id = Guid.NewGuid(), Nome = "Tx B", Preco = 20 }, ct);

                if (fail)
                    throw new InvalidOperationException("Erro proposital para testar rollback.");
            }, ct);

            return Ok(new { ok = true, fail });
        }

        [HttpGet("tx-active")]
        public ActionResult<bool> TxActive()
        {
            return Ok(_repo.HasActiveTransaction());
        }
    }
}
