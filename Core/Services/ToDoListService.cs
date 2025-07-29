using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Core.Services
{
    public class ToDoListService : IToDoListService
    {
        private readonly IToDoListRepository _repository;

        public ToDoListService(IToDoListRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<ToDoList> Add(ToDoUser user, string name, CancellationToken ct)
        {
            await Helper.ValidateString(name, ct);
            if (name.Length > 10)
            throw new ArgumentException("Название списка не может быть длиннее 10 символов");
            if (await _repository.ExistsByName(user.UserId, name, ct))
            throw new InvalidOperationException("Список с таким названием уже существует у этого пользователя");
            var newList = new ToDoList
            {
                Id = Guid.NewGuid(),
                Name = name,
                User = user,
                CreatedAt = DateTime.Now
            };
            await _repository.Add(newList, ct);
            return newList;
        }

        public async Task<ToDoList?> Get(Guid id, CancellationToken ct)
        {
            return await _repository.Get(id, ct);
        }

        public async Task Delete(Guid id, CancellationToken ct)
        {
            await _repository.Delete(id, ct);
        }

        public async Task<IReadOnlyList<ToDoList>> GetUserLists(Guid userId, CancellationToken ct)
        {
            return await _repository.GetByUserId(userId, ct);
        }
    }
}
