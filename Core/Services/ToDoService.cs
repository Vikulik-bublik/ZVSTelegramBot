using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.Exceptions;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.DataAccess;

namespace ZVSTelegramBot.Core.Services
{
    public class ToDoService : IToDoService
    {
        private readonly IToDoRepository _toDoRepository;

        public ToDoService(IToDoRepository toDoRepository)
        {
            _toDoRepository = toDoRepository;
        }

        public async Task<IReadOnlyList<ToDoItem>> GetActiveByUserId(Guid userId, CancellationToken ct)
        {
            return await _toDoRepository.GetActiveByUserId(userId, ct);
        }
        public async Task<IReadOnlyList<ToDoItem>> Find(ToDoUser user, string namePrefix, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(namePrefix))
                return new List<ToDoItem>();
            return await _toDoRepository.Find(user.UserId,
                item => item.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase), ct);
        }

        public async Task<ToDoItem> Add(ToDoUser user, string name, CancellationToken ct)
        {
            Helper.ValidateString(name, ct);
            if (name.Length > Helper.MaxLengthCount)
                throw new TaskLengthLimitException(Helper.MaxLengthCount);
            if (await _toDoRepository.CountActive(user.UserId, ct) >= Helper.MaxTaskCount)
                throw new TaskCountLimitException(Helper.MaxTaskCount);
            if (await _toDoRepository.ExistsByName(user.UserId, name, ct))
                throw new DuplicateTaskException(name);

            var item = new ToDoItem
            {
                Id = Guid.NewGuid(),
                User = user,
                Name = name,
                CreatedAt = DateTime.UtcNow,
                State = ToDoItemState.Active
            };
            await _toDoRepository.Add(item, ct);
            return item;
        }

        public async Task MarkCompleted(Guid id, Guid userId, CancellationToken ct)
        {
            var tasks = await _toDoRepository.GetAllByUserId(userId, ct);
            var item = tasks.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                item.State = ToDoItemState.Completed;
                item.StateChangedAt = DateTime.UtcNow;
                await _toDoRepository.Update(item, ct);
            }
        }

        public async Task Delete(Guid id, CancellationToken ct)
        {
            await _toDoRepository.Delete(id, ct);
        }

        public async Task<IReadOnlyList<ToDoItem>> GetAllTasks(Guid userId, CancellationToken ct)
        {
            return await _toDoRepository.GetAllByUserId(userId, ct);
        }
    }
}
