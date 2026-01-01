using Microsoft.EntityFrameworkCore;
using Karamel.Backend.Data;

namespace Karamel.Backend.Repositories
{
    public class EfRepository<T> : IRepository<T> where T : class
    {
        protected readonly BackendDbContext _db;

        public EfRepository(BackendDbContext db)
        {
            _db = db;
        }

        public virtual async Task AddAsync(T entity)
        {
            await _db.Set<T>().AddAsync(entity);
            await _db.SaveChangesAsync();
        }

        public virtual async Task DeleteAsync(Guid id)
        {
            var entity = await _db.Set<T>().FindAsync(id);
            if (entity is null) return;
            _db.Set<T>().Remove(entity);
            await _db.SaveChangesAsync();
        }

        public virtual async Task<T?> GetByIdAsync(Guid id)
        {
            return await _db.Set<T>().FindAsync(id);
        }

        public virtual async Task<IEnumerable<T>> ListAsync()
        {
            return await _db.Set<T>().ToListAsync();
        }

        public virtual async Task UpdateAsync(T entity)
        {
            _db.Set<T>().Update(entity);
            await _db.SaveChangesAsync();
        }
    }
}
