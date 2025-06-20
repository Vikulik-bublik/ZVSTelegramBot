﻿using System;
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
            _toDoRepository = toDoRepository ?? throw new ArgumentNullException(nameof(toDoRepository));
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
        public async Task<ToDoItem> Add(ToDoUser user, string name, DateTime? deadline, CancellationToken ct)
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
                CreatedAt = DateTime.UtcNow,
                Deadline = deadline,
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
            var task = await _toDoRepository.GetByIdAsync(id, ct);
            if (task == null)
                throw new TaskNotFoundException(id);
            await _toDoRepository.Delete(id, ct);
        }
        public async Task<IReadOnlyList<ToDoItem>> GetAllTasks(Guid userId, CancellationToken ct)
        {
            return await _toDoRepository.GetAllByUserId(userId, ct);
        }
    }
}
