using LinqToDB;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.DataAccess.Models;
using ZVSTelegramBot.Infrastructure.DataAccess;

namespace ZVSTelegramBot.Infrastructure.DataAccess
{
    public class SqlToDoRepository : IToDoRepository
    {
        private readonly IDataContextFactory<ToDoDataContext> _contextFactory;

        public SqlToDoRepository(IDataContextFactory<ToDoDataContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }
        public async Task<IReadOnlyList<ToDoItem>> GetAllByUserId(Guid userId, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var items = await dbContext.ToDoItems
                .LoadWith(i => i.User)
                .LoadWith(i => i.List)
                .LoadWith(i => i.List!.User)
                .Where(i => i.UserId == userId)
                .ToListAsync(ct);
            return items.Select(ModelMapper.MapFromModel).ToList().AsReadOnly();
        }
        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserId(Guid userId, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var items = await dbContext.ToDoItems
                .LoadWith(i => i.User)
                .LoadWith(i => i.List)
                .LoadWith(i => i.List!.User)
                .Where(i => i.UserId == userId && i.State == ToDoItemState.Active)
                .ToListAsync(ct);
            return items.Select(ModelMapper.MapFromModel).ToList().AsReadOnly();
        }
        public async Task<IReadOnlyList<ToDoItem>> Find(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var items = await dbContext.ToDoItems
                .LoadWith(i => i.User)
                .LoadWith(i => i.List)
                .LoadWith(i => i.List!.User)
                .Where(i => i.UserId == userId)
                .ToListAsync(ct);
            return items.Select(ModelMapper.MapFromModel)
                       .Where(predicate)
                       .ToList()
                       .AsReadOnly();
        }
        public async Task Add(ToDoItem item, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var itemModel = ModelMapper.MapToModel(item);
            await dbContext.InsertAsync(itemModel, token: ct);
        }

        public async Task Update(ToDoItem item, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var itemModel = ModelMapper.MapToModel(item);
            await dbContext.UpdateAsync(itemModel, token: ct);
        }
        public async Task Delete(Guid id, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            await dbContext.ToDoItems
                .Where(i => i.Id == id)
                .DeleteAsync(ct);
        }
        public async Task<bool> ExistsByName(Guid userId, string name, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            return await dbContext.ToDoItems
                .AnyAsync(i => i.UserId == userId && i.Name == name, ct);
        }
        public async Task<int> CountActive(Guid userId, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            return await dbContext.ToDoItems
                .CountAsync(i => i.UserId == userId && i.State == ToDoItemState.Active, ct);
        }
        public async Task<ToDoItem?> GetByIdAsync(Guid id, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var itemModel = await dbContext.ToDoItems
                .LoadWith(i => i.User)
                .LoadWith(i => i.List)
                .LoadWith(i => i.List!.User)
                .FirstOrDefaultAsync(i => i.Id == id, ct);
            return ModelMapper.MapFromModel(itemModel);
        }
        public async Task<IReadOnlyList<ToDoItem>> GetActiveWithDeadline(Guid userId, DateTime from, DateTime to, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var items = await dbContext.ToDoItems
                .LoadWith(i => i.User)
                .LoadWith(i => i.List)
                .LoadWith(i => i.List!.User)
                .Where(i => i.UserId == userId
                            && i.State == ToDoItemState.Active
                            && i.Deadline >= from
                            && i.Deadline < to)
                .ToListAsync(ct);
            return items.Select(ModelMapper.MapFromModel).ToList().AsReadOnly();
        }
    }
}
