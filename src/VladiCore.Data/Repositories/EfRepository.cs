using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using VladiCore.Data.Contexts;

namespace VladiCore.Data.Repositories
{
    public class EfRepository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        private readonly VladiCoreContext _context;
        private readonly DbSet<TEntity> _set;

        public EfRepository(VladiCoreContext context)
        {
            _context = context;
            _set = context.Set<TEntity>();
        }

        public IQueryable<TEntity> Query()
        {
            return _set;
        }

        public Task<TEntity> FindAsync(int id)
        {
            return _set.FindAsync(id);
        }

        public async Task AddAsync(TEntity entity)
        {
            _set.Add(entity);
            await Task.CompletedTask;
        }

        public async Task UpdateAsync(TEntity entity)
        {
            _context.Entry(entity).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(TEntity entity)
        {
            _set.Remove(entity);
            await Task.CompletedTask;
        }

        public Task SaveChangesAsync()
        {
            return _context.SaveChangesAsync();
        }
    }
}
