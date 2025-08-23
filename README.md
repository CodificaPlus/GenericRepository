# GenericRepository<T> (EF Core) — Tutorial + Base

**Um repositório genérico simples para Entity Framework Core**, pensado para uso didático em tutoriais e como ponto de partida para contribuições da comunidade.  
O foco é mostrar **padrões comuns** (leitura com/sem tracking, paginação, transações, `CancellationToken`, etc.) de forma direta.

> ⚠️ **Estado do projeto**: intencionalmente minimalista. Melhorias são bem-vindas via PRs e issues.  
> Este código é para **aprendizado**; adapte antes de usar em produção.

---

## Sumário

- [Por que existe?](#por-que-existe)
- [Requisitos](#requisitos)
- [Como usar (exemplos rápidos)](#como-usar-exemplos-rápidos)
- [API do repositório](#api-do-repositório)
- [Conceitos importantes](#conceitos-importantes)
- [Limitações conhecidas](#limitações-conhecidas)
- [Estrutura sugerida](#estrutura-sugerida)
- [Como rodar localmente](#como-rodar-localmente)
- [Contribuindo](#contribuindo)
- [Licença](#licença)

---

## Por que existe?

- Servir de **cola de estudo** para quem está começando com EF Core.
- Demonstrar decisões como:
  - `AsNoTracking` por padrão em leituras;
  - evitar expor `IQueryable` publicamente;
  - uso de `CancellationToken` e `ValueTask`;
  - paginação com `COUNT` + `Skip/Take`;
  - um wrapper simples de **transação**.

---

## Requisitos

- .NET **6+**  
- Entity Framework Core **6+**

---

## Como usar (exemplos rápidos)

### 1) Herde do repositório

```csharp
public class ClienteRepository : GenericRepository<Cliente>
{
    public ClienteRepository(GRContext ctx) : base(ctx) { }
}
```

### 2) Injetar no seu serviço (exemplo com DI)

```csharp
services.AddDbContext<GRContext>(/* ... */);
services.AddScoped<ClienteRepository>();
```

### 3) Ler dados (com filtro, include e ordenação)

```csharp
var clientes = await _repo.QueryAsync(q =>
    q.Where(c => c.Ativo)
     .OrderByDescending(c => c.CriadoEm));
```

### 4) Paginar resultados

```csharp
var (items, total) = await _repo.FindPagedAsync(
    page: 1,
    pageSize: 20,
    predicate: c => c.Ativo,
    orderBy: q => q.OrderBy(c => c.Nome));
```

### 5) CRUD básico

```csharp
await _repo.AddAsync(new Cliente { Id = Guid.NewGuid(), Nome = "Ana" });
await _repo.UpdateAsync(clienteExistente);
await _repo.DeleteAsync(clienteExistente);
```

### 6) Transação simples

```csharp
await _repo.ExecuteInTransactionAsync(async () =>
{
    await _repo.AddAsync(novoCliente1);
    await _repo.AddAsync(novoCliente2);
    // se lançar exceção, a transação faz rollback
});
```

---

## API do repositório

> Assinaturas simplificadas para leitura. Veja o código para detalhes completos.

### Leitura

- **`Task<List<T>> QueryAsync(Func<IQueryable<T>, IQueryable<T>> build, bool asNoTracking = true, CancellationToken ct = default)`**  
  Recebe um **builder** de consulta (com `Where`, `Include`, `OrderBy`, etc.) e materializa em lista.  
  Útil para consultas complexas **sem** expor `IQueryable` para fora da classe.

- **`Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, bool asNoTracking = true, CancellationToken ct = default)`**  
  Filtra por **predicado** (expressão traduzida para SQL) e retorna lista.

- **`ValueTask<T?> FindByIdAsync(Guid id, CancellationToken ct = default)`**  
  Busca por **chave primária Guid** usando `FindAsync`.  
  > **Nota**: limitado a PK `Guid` neste tutorial.

- **`Task<List<T>> FindAllAsync(bool asNoTracking = true, CancellationToken ct = default)`**  
  Retorna **todos** os registros (cuidado em tabelas grandes).

- **`Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)`**  
  Retorna se **existe** ao menos um registro que atenda ao predicado (`AnyAsync`).

- **`Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)`**  
  `COUNT(*)` com/sem filtro.

- **`Task<(List<T> Items, int Total)> FindPagedAsync(int page, int pageSize, Expression<Func<T, bool>>? predicate = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = true, CancellationToken ct = default)`**  
  Paginação em **duas queries**: um `COUNT` total e um `SELECT` paginado com `Skip/Take`.  
  > Recomenda-se sempre fornecer `orderBy` para resultados estáveis.

### Escrita

Cada método chama `SaveChangesAsync` ao final (simples de usar; em cenários de alto volume, considere agrupar operações).

- **`Task AddAsync(T entity, CancellationToken ct = default)`**  
  Adiciona uma entidade.

- **`Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)`**  
  Adiciona várias entidades.

- **`Task UpdateAsync(T entity, CancellationToken ct = default)`**  
  Atualiza a entidade (marca **todas** as propriedades como modificadas).

- **`Task UpdateRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)`**  
  Atualiza várias entidades.

- **`Task DeleteAsync(T entity, CancellationToken ct = default)`**  
  Remove a entidade.

- **`Task<int> SaveChangesAsync(CancellationToken ct = default)`**  
  Encapsula `_context.SaveChangesAsync(ct)`.

### Transações

- **`Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default)`**  
  Executa a `action` dentro de uma transação. Se algo falhar, faz **rollback** e relança a exceção.

- **`bool HasActiveTransaction()`**  
  Indica se há transação ativa no contexto.

### Infra

- **`void Dispose()`**  
  Descarta o `DbContext`.

- **`where T : class, new()`**  
  - `class`: T é tipo de referência (padrão para entidades EF).  
  - `new()`: exige **construtor público sem parâmetros**.

---

## Conceitos importantes

### `AsNoTracking` (leituras mais leves)
- `AsNoTracking()` **não rastreia** as entidades.  
- Vantagens: menos memória e CPU, consultas mais rápidas.  
- Use quando **não** vai editar as entidades **logo depois** da leitura (ou quando usa projeções).

### `IQueryable` x `IEnumerable`
- `IQueryable<T>`: **expressão** que o EF traduz para SQL (execução adiada).  
- `IEnumerable<T>`: dados **já em memória** (execução imediata).  
- Evitar expor `IQueryable` publicamente reduz acoplamento e riscos de executar consultas “acidentais” fora do repositório.  
- **Quando pode usar sem problemas?** Em camadas **internas** (ex.: dentro do próprio repositório ou serviço), quando você **controla** a composição e a execução. Para API pública, prefira **métodos que já materializam** ou **recebem um builder** (como `QueryAsync`), mantendo o repositório no controle.

### `CancellationToken`
- Permite **cancelar** operações assíncronas (ex.: requisição HTTP abortada).  
- Sempre que possível, **propague** o token recebido do chamador.

### `ValueTask`
- Similar a `Task`, mas pode evitar alocação quando o resultado está pronto (ex.: `FindAsync` pega do cache do contexto).  
- Use quando a API já oferece `ValueTask`. Em código próprio, prefira `Task` a menos que haja ganho claro.

### Paginação
- `COUNT` total + `Skip/Take` para pegar a página desejada.  
- **Ordenação** é essencial para evitar resultados “pulando” entre páginas.

### Transações
- Agrupam múltiplas operações em uma **unidade atômica**.  
- Em casos simples de uma operação por vez, as transações implícitas do EF já bastam; para **processos compostos**, use `ExecuteInTransactionAsync`.

---

## Limitações conhecidas

- `FindByIdAsync` assume **PK `Guid`** e **não** suporta chaves compostas.  
- `Update`/`UpdateRange` marcam **todas** as propriedades como modificadas (pode atualizar colunas desnecessárias).  
- O wrapper de transação é **básico** (focado no tutorial).  
- Cada operação de escrita chama `SaveChangesAsync` (simples, mas pode ser menos eficiente em lotes).

> Essas limitações são **propositais** para manter o tutorial direto. A comunidade está convidada a sugerir melhorias.

---

## Estrutura sugerida

```
/VideoGenericRepository
  ├─ Context/
  │   └─ GRContext.cs
  ├─ Repository/
  │   └─ GenericRepository.cs
  ├─ Models/
  │   └─ Cliente.cs
  ├─ README.md   ← este arquivo
  └─ (outros)
```

**`GRContext` (exemplo mínimo)**

```csharp
public class GRContext : DbContext
{
    public GRContext(DbContextOptions<GRContext> options) : base(options) { }

    public DbSet<Cliente> Clientes => Set<Cliente>();
}
```

**`Cliente` (exemplo mínimo)**

```csharp
public class Cliente
{
    public Guid Id { get; set; }
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
```

---

## Como rodar localmente

1. Configure o `DbContext` (SQL Server, SQLite ou InMemory) no seu `Program.cs`:

```csharp
services.AddDbContext<GRContext>(opt =>
    opt.UseInMemoryDatabase("demo")); // para testes rápidos
```

2. Registre seu repositório:

```csharp
services.AddScoped<ClienteRepository>();
```

3. Em um serviço/controlador, injete e use:

```csharp
public class ClientesService
{
    private readonly ClienteRepository _repo;
    public ClientesService(ClienteRepository repo) => _repo = repo;

    public Task<List<Cliente>> AtivosAsync(CancellationToken ct)
        => _repo.FindAsync(c => c.Ativo, asNoTracking: true, ct);
}
```

---

## Contribuindo

- Sinta-se à vontade para abrir **issues** com dúvidas/sugestões.  
- PRs são muito bem-vindos! (documente o **porquê** e inclua testes quando alterar comportamento).  
- Linguagem do repositório: PT-BR nos textos; código em inglês é bem-vindo.

---

## Licença

Este projeto é disponibilizado sob a licença **MIT**. Utilize e modifique livremente, mantendo os créditos.
