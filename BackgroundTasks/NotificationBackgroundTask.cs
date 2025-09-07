using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using ZVSTelegramBot.Core.Services;

namespace ZVSTelegramBot.BackgroundTasks
{
    public class NotificationBackgroundTask : BackgroundTask
    {
        private readonly INotificationService _notificationService;
        private readonly ITelegramBotClient _botClient;

        public NotificationBackgroundTask(INotificationService notificationService, ITelegramBotClient botClient) : base(TimeSpan.FromMinutes(1), nameof(NotificationBackgroundTask))
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
        }

        protected override async Task Execute(CancellationToken ct)
        {
            //получаем нотификации, которые нужно отправить
            var notifications = await _notificationService.GetScheduledNotification(DateTime.UtcNow, ct);

            foreach (var notification in notifications)
            {
                try
                {
                    //отправляем уведомление через бота
                    await _botClient.SendMessage(chatId: notification.User.TelegramUserId, text: notification.Text, cancellationToken: ct);

                    //помечаем нотификацию как отправленную
                    await _notificationService.MarkNotified(notification.Id, ct);

                    Console.WriteLine($"Нотификация отправлена пользователю {notification.User.TelegramUserId}: {notification.Text}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка отправки нотификации {notification.Id}: {ex.Message}");
                }
            }
        }
    }
}
