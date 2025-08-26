using LinqToDB;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.DataAccess.Models;
using ZVSTelegramBot.Infrastructure.DataAccess;

namespace ZVSTelegramBot.Infrastructure.DataAccess
{
    public class SqlToDoListRepository : IToDoListRepository
    {
        private readonly IDataContextFactory<ToDoDataContext> _contextFactory;

        public SqlToDoListRepository(IDataContextFactory<ToDoDataContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }
        public async Task<ToDoList?> Get(Guid id, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var listModel = await dbContext.ToDoLists
                .LoadWith(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == id, ct);
            return ModelMapper.MapFromModel(listModel);
        }
        public async Task<IReadOnlyList<ToDoList>> GetByUserId(Guid userId, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var listModels = await dbContext.ToDoLists
                .LoadWith(l => l.User)
                .Where(l => l.UserId == userId)
                .ToListAsync(ct);
            return listModels.Select(ModelMapper.MapFromModel).ToList().AsReadOnly();
        }
        public async Task Add(ToDoList list, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var listModel = ModelMapper.MapToModel(list);
            await dbContext.InsertAsync(listModel, token: ct);
        }
        public async Task Delete(Guid id, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            await dbContext.ToDoLists
                .Where(l => l.Id == id)
                .DeleteAsync(ct);
        }
        public async Task<bool> ExistsByName(Guid userId, string name, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            return await dbContext.ToDoLists
                .AnyAsync(l => l.UserId == userId && l.Name == name, ct);
        }
    }
}
