using LinqToDB;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.DataAccess.Models;
using ZVSTelegramBot.Infrastructure.DataAccess;

namespace ZVSTelegramBot.Infrastructure.DataAccess
{
    public class SqlUserRepository : IUserRepository
    {
        private readonly IDataContextFactory<ToDoDataContext> _contextFactory;
        public SqlUserRepository(IDataContextFactory<ToDoDataContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }
        public async Task<ToDoUser?> GetUser(Guid userId, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var userModel = await dbContext.ToDoUsers
                .FirstOrDefaultAsync(u => u.UserId == userId, ct);
            return ModelMapper.MapFromModel(userModel);
        }
        public async Task<ToDoUser?> GetUserByTelegramUserId(long telegramUserId, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var userModel = await dbContext.ToDoUsers
                .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
            return ModelMapper.MapFromModel(userModel);
        }
        public async Task Add(ToDoUser user, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var userModel = ModelMapper.MapToModel(user);
            await dbContext.InsertAsync(userModel, token: ct);
        }
        public async Task UpdateUser(ToDoUser user, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var userModel = ModelMapper.MapToModel(user);
            await dbContext.UpdateAsync(userModel, token: ct);
        }
        public async Task<IReadOnlyList<ToDoUser>> GetUsers(CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();
            var userModels = await dbContext.ToDoUsers.ToListAsync(ct);
            return userModels.Select(ModelMapper.MapFromModel).ToList().AsReadOnly();
        }
    }
}
