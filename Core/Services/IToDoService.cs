using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleBot.Core.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsoleBot.Core.Services
{
    public interface IToDoService
    {
        Task<IReadOnlyList<ToDoItem>> GetActiveByUserId(Guid userId, CancellationToken ct);
        Task<IReadOnlyList<ToDoItem>> Find(ToDoUser user, string namePrefix, CancellationToken ct);
        Task<ToDoItem> Add(ToDoUser user, string name, CancellationToken ct);
        Task MarkCompleted(Guid id, Guid userId, CancellationToken ct);
        Task Delete(Guid id, CancellationToken ct);
        Task<IReadOnlyList<ToDoItem>> GetAllTasks(Guid userId, CancellationToken ct);
    }
}
