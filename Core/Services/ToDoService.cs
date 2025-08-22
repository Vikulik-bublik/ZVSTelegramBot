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
            return await _toDoRepository.Find(user.UserId, item => item.Name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase), ct);
        }
        public async Task<ToDoItem> Add(ToDoUser user, string name, DateTime? deadline, ToDoList? list, CancellationToken ct)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            Helper.ValidateString(name, ct);
            name = name.Trim();
            if (await _toDoRepository.ExistsByName(user.UserId, name, ct))
                throw new DuplicateTaskException(name);
            var item = new ToDoItem
            {
                Id = Guid.NewGuid(),
                User = user,
                Name = name,
                List = list,
                CreatedAt = DateTime.Now,
                Deadline = deadline,
                State = ToDoItemState.Active
            };
            await _toDoRepository.Add(item, ct);
            return item;
        }
        public async Task MarkCompleted(Guid id, Guid userId, CancellationToken ct)
        {
            var item = await _toDoRepository.GetByIdAsync(id, ct);
            if (item == null)
                throw new TaskNotFoundException(id);
            if (item != null && item.User.UserId == userId)
            {
                item.State = ToDoItemState.Completed;
                item.StateChangedAt = DateTime.Now;
                await _toDoRepository.Update(item, ct);
            }
        }
        public async Task Delete(Guid id, CancellationToken ct)
        {
            var task = await _toDoRepository.GetByIdAsync(id, ct);
            if (task == null)
                throw new TaskNotFoundException(id);
            await _toDoRepository.Delete(id, ct);
        }
        public async Task<IReadOnlyList<ToDoItem>> GetAllTasks(Guid userId, CancellationToken ct)
        {
            return await _toDoRepository.GetAllByUserId(userId, ct);
        }
        public async Task<IReadOnlyList<ToDoItem>> GetByUserIdAndList(Guid userId, Guid? listId, CancellationToken ct)
        {
            var allTasks = await _toDoRepository.GetAllByUserId(userId, ct);

            return listId == null
            ? allTasks.Where(t => t.List == null).ToList()
            : allTasks.Where(t => t.List?.Id == listId).ToList();
        }
        public async Task<ToDoItem?> Get(Guid toDoItemId, CancellationToken ct)
        {
            return await _toDoRepository.GetByIdAsync(toDoItemId, ct);
        }
    }
}
