using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.Services;
using ZVSTelegramBot.Core.Entities;
using ZVSTelegramBot.Core.DataAccess.Models;
using ZVSTelegramBot.Infrastructure.DataAccess;
using LinqToDB;

namespace ZVSTelegramBot.Infrastructure
{
    public class NotificationService : INotificationService
    {
        private readonly IDataContextFactory<ToDoDataContext> _contextFactory;

        public NotificationService(IDataContextFactory<ToDoDataContext> contextFactory)
        {
            _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        }

        public async Task<bool> ScheduleNotification(Guid userId, string type, string text, DateTime scheduledAt, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();

            //проверяем, существует ли уже нотификация с таким userId и type
            var existingNotification = await dbContext.Notifications
                .FirstOrDefaultAsync(n => n.UserId == userId && n.Type == type, ct);

            if (existingNotification != null)
            {
                return false;
            }

            //создаем новую нотификацию
            var notification = new NotificationModel
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = type,
                Text = text,
                ScheduledAt = scheduledAt,
                IsNotified = false,
                NotifiedAt = null
            };

            //добавляем в базу данных
            await dbContext.InsertAsync(notification, token: ct);
            return true;
        }

        public async Task<IReadOnlyList<Notification>> GetScheduledNotification(DateTime scheduledBefore, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();

            var notificationModels = await dbContext.Notifications
                .LoadWith(n => n.User)
                .Where(n => !n.IsNotified && n.ScheduledAt <= scheduledBefore)
                .ToListAsync(ct);

            return notificationModels.Select(ModelMapper.MapFromModel).ToList().AsReadOnly();
        }

        public async Task MarkNotified(Guid notificationId, CancellationToken ct)
        {
            using var dbContext = _contextFactory.CreateDataContext();

            await dbContext.Notifications
                .Where(n => n.Id == notificationId)
                .Set(n => n.IsNotified, true)
                .Set(n => n.NotifiedAt, DateTime.UtcNow)
                .UpdateAsync(ct);
        }
    }
}
