using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.Entities;

namespace ZVSTelegramBot.Core.Services
{
    public interface INotificationService
    {
        /// <summary>
        /// Создает нотификацию. Если запись с userId и type уже есть, то вернуть false и не добавлять запись, иначе вернуть true
        /// </summary>
        Task<bool> ScheduleNotification(
            Guid userId,
            string type,
            string text,
            DateTime scheduledAt,
            CancellationToken ct);

        /// <summary>
        /// Возвращает нотификации, у которых IsNotified = false && ScheduledAt <= scheduledBefore
        /// </summary>
        Task<IReadOnlyList<Notification>> GetScheduledNotification(DateTime scheduledBefore, CancellationToken ct);

        /// <summary>
        /// Помечает нотификацию как отправленную
        /// </summary>
        Task MarkNotified(Guid notificationId, CancellationToken ct);
    }
}
