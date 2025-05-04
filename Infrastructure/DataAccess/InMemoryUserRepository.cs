using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleBot.Core.DataAccess;
using ConsoleBot.Core.Entities;

namespace ConsoleBot.Infrastructure.DataAccess
{
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly List<ToDoUser> _users = new();

        public Task<ToDoUser?> GetUser(Guid userId, CancellationToken ct)
        {
            return Task.FromResult(_users.FirstOrDefault(user => user.UserId == userId));
        }

        public Task<ToDoUser?> GetUserByTelegramUserId(long telegramUserId, CancellationToken ct)
        {
            return Task.FromResult(_users.FirstOrDefault(user => user.TelegramUserId == telegramUserId));
        }

        public Task Add(ToDoUser user, CancellationToken ct)
        {
            if (_users.Any(u => u.TelegramUserId == user.TelegramUserId))
            {
                throw new InvalidOperationException("Пользователь с этим ID уже существует.");
            }
            _users.Add(user);
            return Task.CompletedTask;
        }
    }
}
