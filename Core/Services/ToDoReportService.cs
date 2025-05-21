using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Core.Services
{
    public class ToDoReportService : IToDoReportService
    {
        private readonly IToDoRepository _toDoRepository;
        public ToDoReportService(IToDoRepository toDoRepository)
        {
            _toDoRepository = toDoRepository;
        }
        public async Task<(int total, int completed, int active, DateTime generatedAt)> GetUserStats(Guid userId, CancellationToken ct)
        {
            var tasks = await _toDoRepository.GetAllByUserId(userId, ct);
            return (
            total: tasks.Count, 
            completed: tasks.Count(t => t.State == ToDoItemState.Completed), 
            active: tasks.Count(t => t.State == ToDoItemState.Active), 
            generatedAt: DateTime.UtcNow
            );
        }
    }
}
