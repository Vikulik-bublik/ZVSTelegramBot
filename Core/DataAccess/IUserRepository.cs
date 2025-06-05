using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Core.DataAccess
{
    public interface IUserRepository
    {
        Task<ToDoUser?> GetUser(Guid userId, CancellationToken ct);
        Task<ToDoUser?> GetUserByTelegramUserId(long telegramUserId, CancellationToken ct);
        Task Add(ToDoUser user, CancellationToken ct);
        Task UpdateUser(ToDoUser user, CancellationToken ct);
    }
}
