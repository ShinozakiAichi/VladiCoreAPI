using Microsoft.EntityFrameworkCore;
using VladiCore.Data.Contexts;

namespace VladiCore.Data.Repositories;

public class EfRepository<TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly AppDbContext _context;
    private readonly DbSet<TEntity> _set;

    public EfRepository(AppDbContext context)
    {
        _context = context;
        _set = context.Set<TEntity>();
    }

    public IQueryable<TEntity> Query()
    {
        return _set;
    }

    public async Task<TEntity?> FindAsync(int id)
    {
        return await _set.FindAsync(id);
    }

    public async Task AddAsync(TEntity entity)
    {
        await _set.AddAsync(entity);
    }

    public Task UpdateAsync(TEntity entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TEntity entity)
    {
        _set.Remove(entity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
