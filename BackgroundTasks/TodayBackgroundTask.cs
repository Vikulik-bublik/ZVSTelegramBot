using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Services;

namespace ZVSTelegramBot.BackgroundTasks
{
    public class TodayBackgroundTask : BackgroundTask
    {
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _userRepository;
        private readonly IToDoRepository _toDoRepository;

        public TodayBackgroundTask(INotificationService notificationService, IUserRepository userRepository, IToDoRepository toDoRepository) : base(TimeSpan.FromDays(1), nameof(TodayBackgroundTask))
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _toDoRepository = toDoRepository ?? throw new ArgumentNullException(nameof(toDoRepository));
        }

        protected override async Task Execute(CancellationToken ct)
        {
            //получаем всех пользователей
            var users = await _userRepository.GetUsers(ct);

            //определяем диапазон для сегодняшнего дня
            var todayStart = DateTime.UtcNow.Date;
            var todayEnd = todayStart.AddDays(1);

            foreach (var user in users)
            {
                //получаем задачи пользователя на сегодня
                var tasks = await _toDoRepository.GetActiveWithDeadline(user.UserId, todayStart, todayEnd, ct);

                if (tasks.Count > 0)
                {
                    //формируем текст уведомления
                    var messageBuilder = new StringBuilder();
                    messageBuilder.AppendLine("📅 Ваши задачи на сегодня:");

                    foreach (var task in tasks)
                    {
                        messageBuilder.AppendLine($"• {task.Name}");

                        if (task.Deadline.HasValue)
                        {
                            var deadlineTime = task.Deadline.Value.ToString("HH:mm");
                            messageBuilder.AppendLine($"  🕘 {deadlineTime}");
                        }
                    }

                    //создаем нотификацию
                    await _notificationService.ScheduleNotification(
                        user.UserId,
                        $"Today_{DateOnly.FromDateTime(DateTime.UtcNow)}",
                        messageBuilder.ToString(),
                        DateTime.UtcNow,
                        ct);
                }
            }
        }
    }
}
