using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZVSTelegramBot.Core.Services
{
    public interface IToDoReportService
    {
        Task<(int total, int completed, int active, DateTime generatedAt)> GetUserStats(Guid userId, CancellationToken ct);
    }
}
