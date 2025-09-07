using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZVSTelegramBot.Core.DataAccess;
using ZVSTelegramBot.Core.Services;

namespace ZVSTelegramBot.BackgroundTasks
{
    public class DeadlineBackgroundTask : BackgroundTask
    {
        private readonly INotificationService _notificationService;
        private readonly IUserRepository _userRepository;
        private readonly IToDoRepository _toDoRepository;

        public DeadlineBackgroundTask(INotificationService notificationService, IUserRepository userRepository, IToDoRepository toDoRepository) : base(TimeSpan.FromHours(1), nameof(DeadlineBackgroundTask))
        {
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _toDoRepository = toDoRepository ?? throw new ArgumentNullException(nameof(toDoRepository));
        }

        protected override async Task Execute(CancellationToken ct)
        {
            //получаем всех пользователей
            var users = await _userRepository.GetUsers(ct);

            foreach (var user in users)
            {
                //для каждого пользователя получаем задачи с дедлайном за вчера
                var yesterday = DateTime.UtcNow.AddDays(-1).Date;
                var today = DateTime.UtcNow.Date;

                var tasks = await _toDoRepository.GetActiveWithDeadline(user.UserId, yesterday, today, ct);

                foreach (var task in tasks)
                {
                    //создаем нотификацию
                    await _notificationService.ScheduleNotification(
                        user.UserId,
                        $"Deadline_{task.Id}",
                        $"Ой! Вы пропустили дедлайн по задаче {task.Name}",
                        DateTime.UtcNow,
                        ct);
                }
            }
        }
    }
}
