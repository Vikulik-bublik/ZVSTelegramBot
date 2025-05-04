using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleBot.Core.DataAccess;
using ConsoleBot.Core.Entities;

namespace ConsoleBot.Infrastructure.DataAccess
{
    public class InMemoryToDoRepository : IToDoRepository
    {
        private readonly List<ToDoItem> _items = new();

        public Task<IReadOnlyList<ToDoItem>> GetAllByUserId(Guid userId, CancellationToken ct)
        {
            IReadOnlyList<ToDoItem> result = _items.Where(item => item.User.UserId == userId).ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<ToDoItem>> GetActiveByUserId(Guid userId, CancellationToken ct)
        {
            IReadOnlyList<ToDoItem> result = _items.Where(item => item.User.UserId == userId && item.State == ToDoItemState.Active).ToList();
            return Task.FromResult(result);
        }
        public Task<IReadOnlyList<ToDoItem>> Find(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct)
        {
            IReadOnlyList<ToDoItem> result = _items.Where(item => item.User.UserId == userId && predicate(item)).ToList();
            return Task.FromResult(result);
        }
        public Task Add(ToDoItem item, CancellationToken ct)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            _items.Add(item);
            return Task.CompletedTask;
        }

        public Task Update(ToDoItem item, CancellationToken ct)
        {
            var existingItem = _items.FirstOrDefault(i => i.Id == item.Id);
            if (existingItem != null)
            {
                existingItem.Name = item.Name;
                existingItem.State = item.State;
                existingItem.StateChangedAt = item.StateChangedAt;
            }
            return Task.CompletedTask;
        }

        public Task Delete(Guid id, CancellationToken ct)
        {
            var item = _items.FirstOrDefault(i => i.Id == id);
            if (item != null)
            {
                _items.Remove(item);
            }
            return Task.CompletedTask;
        }

        public Task<bool> ExistsByName(Guid userId, string name, CancellationToken ct)
        {
            bool result = _items.Any(item =>
                item.User.UserId == userId &&
                item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(result);
        }

        public Task<int> CountActive(Guid userId, CancellationToken ct)
        {
            int result = _items.Count(item =>
                item.User.UserId == userId &&
                item.State == ToDoItemState.Active);
            return Task.FromResult(result);
        }
    }
}
