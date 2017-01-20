using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace TodoList.ReadModel
{
    public class ReadModelFacade : IReadModelFacade
    {
        private readonly Func<ReadModelDbContext> _dbContextFactory;

        public ReadModelFacade(Func<ReadModelDbContext> dbContextFactory)
        {
            if (dbContextFactory == null)
                throw new ArgumentNullException(nameof(dbContextFactory));

            _dbContextFactory = dbContextFactory;
        }

        public async Task<IEnumerable<TodoItem>> GetAllItems()
        {
            using (ReadModelDbContext db = _dbContextFactory.Invoke())
            {
                return await db
                    .TodoItems
                    .AsNoTracking()
                    .OrderByDescending(e => e.SequenceId)
                    .ToListAsync();
            }
        }

        public async Task<TodoItem> Find(Guid id)
        {
            using (ReadModelDbContext db = _dbContextFactory.Invoke())
            {
                return await db
                    .TodoItems
                    .AsNoTracking()
                    .Where(e => e.Id == id)
                    .SingleOrDefaultAsync();
            }
        }
    }
}
