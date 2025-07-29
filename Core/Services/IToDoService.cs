using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Core.Services
{
    public interface IToDoService
    {
        Task<IReadOnlyList<ToDoItem>> GetActiveByUserId(Guid userId, CancellationToken ct);
        Task<IReadOnlyList<ToDoItem>> Find(ToDoUser user, string namePrefix, CancellationToken ct);
        Task<ToDoItem> Add(ToDoUser user, string name, DateTime? deadline, ToDoList? list, CancellationToken ct);
        Task MarkCompleted(Guid id, Guid userId, CancellationToken ct);
        Task Delete(Guid id, CancellationToken ct);
        Task<IReadOnlyList<ToDoItem>> GetAllTasks(Guid userId, CancellationToken ct);
        Task<IReadOnlyList<ToDoItem>> GetByUserIdAndList(Guid userId, Guid? listId, CancellationToken ct);
    }
}
