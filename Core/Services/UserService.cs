using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Core.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        public UserService(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        public async Task<ToDoUser> RegisterUser(long telegramUserId, string telegramUserName, CancellationToken ct)
        {
            var user = new ToDoUser
            {
                UserId = Guid.NewGuid(),
                TelegramUserId = telegramUserId,
                TelegramUserName = telegramUserName,
                RegisteredAt = DateTime.Now
            };
            await _userRepository.Add(user, ct);
            return user;
        }
        public async Task<ToDoUser?> GetUser(long telegramUserId, CancellationToken ct)
        {
            return await _userRepository.GetUserByTelegramUserId(telegramUserId, ct);
        }
        public async Task UpdateUser(ToDoUser user, CancellationToken ct)
        {
            await _userRepository.UpdateUser(user, ct);
        }
    }
}
