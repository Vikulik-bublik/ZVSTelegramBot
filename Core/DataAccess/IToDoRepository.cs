using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Core.DataAccess
{
    public interface IToDoRepository
    {
        Task<IReadOnlyList<ToDoItem>> GetAllByUserId(Guid userId, CancellationToken ct);
        Task<IReadOnlyList<ToDoItem>> GetActiveByUserId(Guid userId, CancellationToken ct);
        Task<IReadOnlyList<ToDoItem>> Find(Guid userId, Func<ToDoItem, bool> predicate, CancellationToken ct);
        Task Add(ToDoItem item, CancellationToken ct);
        Task Update(ToDoItem item, CancellationToken ct);
        Task Delete(Guid userId, Guid id, CancellationToken ct);
        Task<bool> ExistsByName(Guid userId, string name, CancellationToken ct);
        Task<int> CountActive(Guid userId, CancellationToken ct);
        Task<ToDoItem?> GetByIdAsync(Guid userId, Guid id, CancellationToken ct);
    }
}
